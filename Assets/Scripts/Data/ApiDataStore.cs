using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 联网实现：数据存在服务器 MySQL 里。
///
/// 设计要点（P3）：
/// IDataStore 是同步接口（UI 每次刷新都直接调用），但网络请求必然异步。
/// 所以 ApiDataStore 本地维护一份 PlayerProfile 镜像：
///   读 -> 走内存镜像，同步返回，UI 完全不用改
///   写 -> 先更新镜像让 UI 立刻有反馈，再异步把变更推到服务器；
///          服务器拒绝就回滚镜像并触发 OnCoinsChanged，UI 自动纠正。
/// 这样既保住了 P1 收口的成果，也不用把 17 个 UI 文件改成 async。
///
/// P4：金币不再由客户端上报最终值。所有扣费走 SpendCoins，
/// 服务端用 SELECT ... FOR UPDATE 行锁 + 事务完成扣币与写入，
/// 改本地文件已经刷不出金币了。
/// P4 待办只剩：把「同一局内不能重复买同一件商品」这类单局规则也搬到服务端。
/// </summary>
public class ApiDataStore : IDataStore
{
    private readonly ApiClient api;
    private readonly PlayerProfile cache = new PlayerProfile();

    // 正在向服务器推送时，避免远程回调把本地刚改的值又覆盖回去
    private bool applyingRemote;

    public PlayerProfile CachedProfile => cache;
    public bool IsOnline => true;

    public event Action<int> OnCoinsChanged;

    /// <summary>
    /// 记录 API 客户端引用并加载缓存的账号档案。
    /// </summary>
    public ApiDataStore(ApiClient api)
    {
        this.api = api;
    }

    /// <summary>用服务端返回的档案覆盖本地镜像（登录成功 / 手动刷新时调用）。</summary>
    public void ApplyServerProfile(ProfileData data)
    {
        if (data == null) return;

        applyingRemote = true;
        try
        {
            cache.coins = data.coins;

            cache.ownedCharacters = ToList(data.characters, c => c.uid);
            cache.unlockedEngravings = data.engravings != null ? ToList(data.engravings.unlocked, x => x) : new List<string>();
            cache.selectedEngravings = data.engravings != null ? ToList(data.engravings.selected, x => x) : new List<string>();
            cache.sessionSnapshot = data.sessionSnapshot ?? string.Empty;
        }
        finally
        {
            applyingRemote = false;
        }

        NotifyCoinsChanged(cache.coins);
    }

    // ---- 金币 ----

    public int GetCoins() => cache.coins;

    /// <summary>
    /// 设置金币数，写入成功后广播变化。
    /// </summary>
    public void SetCoins(int value)
    {
        value = Mathf.Max(0, value);
        int previous = cache.coins;
        if (previous == value) return;

        cache.coins = value;
        NotifyCoinsChanged(value);

        api.SaveCoins(value, envelope =>
        {
            if (IsSuccess(envelope)) return;
            Rollback(() => cache.coins = previous);
            Debug.LogWarning("[ApiDataStore] 金币写回服务器失败，已回滚本地缓存");
        });
    }

    public void AddCoins(int delta) => SetCoins(GetCoins() + delta);

    // ---- 消费（服务端权威：扣费与权益写入在同一事务里完成） ----

    /// <summary>
    /// 扣费购买：先本地扣款，服务器写入失败则回滚并回调结果。
    /// </summary>
    public void SpendCoins(SpendOrder order, Action<PurchaseOutcome> onResult)
    {
        if (order == null || string.IsNullOrEmpty(order.ItemId))
        {
            onResult?.Invoke(PurchaseOutcome.Fail("订单无效"));
            return;
        }

        // 先做本地余额预判，省掉一次必然失败的请求；真正的判定仍在服务端
        if (GetCoins() - order.Cost + order.GrantsCoins < 0)
        {
            onResult?.Invoke(PurchaseOutcome.Fail("金币不足"));
            return;
        }

        var payload = new SpendPayload
        {
            itemId = order.ItemId,
            cost = order.Cost,
            grantsCharacterUid = order.GrantsCharacterUid,
            unlocksEngraving = order.UnlocksEngraving,
            grantsCoins = order.GrantsCoins
        };

        api.SpendCoins(payload, envelope =>
        {
            if (envelope == null)
            {
                onResult?.Invoke(PurchaseOutcome.Fail("网络异常，购买未完成"));
                return;
            }

            if (envelope.code != 0)
            {
                onResult?.Invoke(PurchaseOutcome.Fail(envelope.message));
                return;
            }

            // 服务端成功：本地镜像以服务端为准
            cache.coins = envelope.data.coins;
            if (!string.IsNullOrEmpty(order.GrantsCharacterUid) && !cache.ownedCharacters.Contains(order.GrantsCharacterUid))
                cache.ownedCharacters.Add(order.GrantsCharacterUid);
            if (!string.IsNullOrEmpty(order.UnlocksEngraving) && !cache.unlockedEngravings.Contains(order.UnlocksEngraving))
                cache.unlockedEngravings.Add(order.UnlocksEngraving);

            NotifyCoinsChanged(cache.coins);
            onResult?.Invoke(PurchaseOutcome.Ok(cache.coins));
        });
    }

    /// <summary>
    /// 结算本局金币收益并同步到服务器。
    /// </summary>
    public void SettleRunReward(int coinsGained)
    {
        if (coinsGained <= 0) return;

        api.SettleRun(coinsGained, envelope =>
        {
            if (envelope == null || envelope.code != 0)
            {
                Debug.LogWarning("[ApiDataStore] 单局结算写回失败，金币可能有偏差");
                return;
            }

            cache.coins = envelope.data.coins;
            NotifyCoinsChanged(cache.coins);
        });
    }

    // ---- 角色拥有 ----

    /// <summary>
    /// 判断角色是否已拥有。
    /// </summary>
    public bool IsCharacterOwned(string characterUid) =>
        !string.IsNullOrEmpty(characterUid) && cache.ownedCharacters.Contains(characterUid);

    /// <summary>
    /// 更新角色拥有状态并同步到服务器。
    /// </summary>
    public void SetCharacterOwned(string characterUid, bool owned)
    {
        if (string.IsNullOrEmpty(characterUid)) return;

        bool had = cache.ownedCharacters.Contains(characterUid);
        if (had == owned) return;

        if (owned) cache.ownedCharacters.Add(characterUid);
        else cache.ownedCharacters.Remove(characterUid);

        api.SaveCharacterOwned(characterUid, owned, envelope =>
        {
            if (IsSuccess(envelope)) return;
            Rollback(() =>
            {
                if (owned) cache.ownedCharacters.Remove(characterUid);
                else cache.ownedCharacters.Add(characterUid);
            });
            Debug.LogWarning($"[ApiDataStore] 角色拥有状态写回失败：{characterUid}");
        });
    }

    // ---- 本局进度（跟着账号走：换号不留，重登还在） ----

    public string GetSessionSnapshot() => cache.sessionSnapshot ?? string.Empty;

    /// <summary>
    /// 把本局快照保存到服务器。
    /// </summary>
    public void SetSessionSnapshot(string snapshot)
    {
        string value = snapshot ?? string.Empty;
        string previous = cache.sessionSnapshot;
        if (previous == value) return;

        cache.sessionSnapshot = value;

        api.SaveSessionSnapshot(value, envelope =>
        {
            if (IsSuccess(envelope)) return;
            cache.sessionSnapshot = previous;
            Debug.LogWarning("[ApiDataStore] 本局进度写回服务器失败，已回滚本地缓存");
        });
    }

    // ---- 铭文 ----

    public IReadOnlyList<string> GetUnlockedEngravings() => cache.unlockedEngravings;

    /// <summary>
    /// 解锁铭文并同步到服务器。
    /// </summary>
    public void UnlockEngraving(string engravingId)
    {
        if (string.IsNullOrEmpty(engravingId) || cache.unlockedEngravings.Contains(engravingId)) return;

        cache.unlockedEngravings.Add(engravingId);

        api.UnlockEngraving(engravingId, envelope =>
        {
            if (IsSuccess(envelope)) return;
            Rollback(() => cache.unlockedEngravings.Remove(engravingId));
            Debug.LogWarning($"[ApiDataStore] 铭文解锁写回失败：{engravingId}");
        });
    }

    public IReadOnlyList<string> GetSelectedEngravings() => cache.selectedEngravings;

    /// <summary>
    /// 保存当前装备的铭文并同步到服务器。
    /// </summary>
    public void SetSelectedEngravings(IReadOnlyList<string> engravingIds)
    {
        var ids = engravingIds == null ? new List<string>() : new List<string>(engravingIds);
        var previous = new List<string>(cache.selectedEngravings);
        cache.selectedEngravings = ids;

        api.SelectEngravings(ids.ToArray(), envelope =>
        {
            if (IsSuccess(envelope)) return;
            Rollback(() => cache.selectedEngravings = previous);
            Debug.LogWarning("[ApiDataStore] 铭文装备列表写回失败，已回滚");
        });
    }

    public void ClearSelectedEngravings() => SetSelectedEngravings(null);

    // ---- 辅助方法 ----

    private static bool IsSuccess<T>(T envelope) where T : class
    {
        if (envelope == null) return false;

        // 服务端统一 { code, message, data }，这里用反射避免为每种 envelope 各写一份判断
        var field = envelope.GetType().GetField("code");
        if (field == null) return false;
        return Convert.ToInt32(field.GetValue(envelope)) == 0;
    }

    /// <summary>
    /// 服务器写入失败时回滚本地改动。
    /// </summary>
    private void Rollback(Action revert)
    {
        applyingRemote = true;
        try { revert(); }
        finally { applyingRemote = false; }
        NotifyCoinsChanged(cache.coins);
    }

    /// <summary>
    /// 广播金币变化事件。
    /// </summary>
    private void NotifyCoinsChanged(int value)
    {
        if (applyingRemote && value == cache.coins) return;
        var handler = OnCoinsChanged;
        if (handler != null) handler.Invoke(value);
    }

    private static List<string> ToList<T>(T[] source, Func<T, string> selector)
    {
        var list = new List<string>();
        if (source == null) return list;
        foreach (var item in source)
        {
            if (item == null) continue;
            string value = selector(item);
            if (!string.IsNullOrEmpty(value)) list.Add(value);
        }
        return list;
    }
}
