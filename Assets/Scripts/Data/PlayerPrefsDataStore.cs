using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 本地 PlayerPrefs 实现。
/// 键名与旧版完全一致（Coins / CharOwned_* / Engravings_Unlocked / Engravings_Selected），
/// 保证 P1 改造后行为不变、老存档直接兼容。
/// 后续作为离线兜底实现保留。
/// </summary>
public class PlayerPrefsDataStore : IDataStore
{
    private const string CoinsKey = "Coins";
    private const string CharOwnedPrefix = "CharOwned_";
    private const string EngravingsUnlockedKey = "Engravings_Unlocked";
    private const string EngravingsSelectedKey = "Engravings_Selected";
    private const string SessionSnapshotKey = "Run_Session";

    private readonly PlayerProfile cached = new PlayerProfile();

    public PlayerProfile CachedProfile => cached;
    public bool IsOnline => false;

    public event Action<int> OnCoinsChanged;

    /// <summary>
    /// 读取本地存档并填充内存缓存。
    /// </summary>
    public PlayerPrefsDataStore()
    {
        cached.coins = PlayerPrefs.GetInt(CoinsKey, 0);
    }

    // ---- 金币 ----

    /// <summary>
    /// 读取本地缓存的金币数。
    /// </summary>
    public int GetCoins()
    {
        return PlayerPrefs.GetInt(CoinsKey, 0);
    }

    /// <summary>
    /// 写入金币数并广播变化。
    /// </summary>
    public void SetCoins(int value)
    {
        value = Mathf.Max(0, value);
        PlayerPrefs.SetInt(CoinsKey, value);
        PlayerPrefs.Save();
        cached.coins = value;
        NotifyCoinsChanged(value);
    }

    /// <summary>
    /// 在现有金币上增减并保存。
    /// </summary>
    public void AddCoins(int delta)
    {
        SetCoins(GetCoins() + delta);
    }

    // ---- 消费（本地版：按与服务端完全相同的规则执行同一份订单） ----

    /// <summary>
    /// 扣费购买，失败时回滚并回调结果。
    /// </summary>
    public void SpendCoins(SpendOrder order, Action<PurchaseOutcome> onResult)
    {
        if (order == null || string.IsNullOrEmpty(order.ItemId))
        {
            onResult?.Invoke(PurchaseOutcome.Fail("订单无效"));
            return;
        }

        int balance = GetCoins() - order.Cost + order.GrantsCoins;
        if (balance < 0)
        {
            onResult?.Invoke(PurchaseOutcome.Fail("金币不足"));
            return;
        }

        // 扣费与权益写入一次做完，顺序和服务端事务保持一致
        SetCoins(balance);
        if (!string.IsNullOrEmpty(order.GrantsCharacterUid)) SetCharacterOwned(order.GrantsCharacterUid, true);
        if (!string.IsNullOrEmpty(order.UnlocksEngraving)) UnlockEngraving(order.UnlocksEngraving);

        onResult?.Invoke(PurchaseOutcome.Ok(GetCoins()));
    }

    /// <summary>
    /// 把本局金币收益加到总额上并保存。
    /// </summary>
    public void SettleRunReward(int coinsGained)
    {
        if (coinsGained <= 0) return;
        AddCoins(coinsGained);
    }

    // ---- 角色拥有 ----

    /// <summary>
    /// 判断角色是否已购买。
    /// </summary>
    public bool IsCharacterOwned(string characterUid)
    {
        if (string.IsNullOrEmpty(characterUid)) return false;
        bool owned = PlayerPrefs.GetInt(CharOwnedPrefix + characterUid, 0) == 1;
        SyncCacheList(cached.ownedCharacters, characterUid, owned);
        return owned;
    }

    /// <summary>
    /// 记录角色的购买状态。
    /// </summary>
    public void SetCharacterOwned(string characterUid, bool owned)
    {
        PlayerPrefs.SetInt(CharOwnedPrefix + characterUid, owned ? 1 : 0);
        PlayerPrefs.Save();
        SyncCacheList(cached.ownedCharacters, characterUid, owned);
    }

    // ---- 本局进度（离线档一份，联网时由服务器存） ----

    /// <summary>
    /// 读取本局快照 JSON。
    /// </summary>
    public string GetSessionSnapshot()
    {
        cached.sessionSnapshot = PlayerPrefs.GetString(SessionSnapshotKey, string.Empty);
        return cached.sessionSnapshot;
    }

    /// <summary>
    /// 写入本局快照 JSON。
    /// </summary>
    public void SetSessionSnapshot(string snapshot)
    {
        string value = snapshot ?? string.Empty;
        if (string.IsNullOrEmpty(value)) PlayerPrefs.DeleteKey(SessionSnapshotKey);
        else PlayerPrefs.SetString(SessionSnapshotKey, value);
        PlayerPrefs.Save();
        cached.sessionSnapshot = value;
    }

    // ---- 铭文 ----

    /// <summary>
    /// 读取已解锁的铭文列表。
    /// </summary>
    public IReadOnlyList<string> GetUnlockedEngravings()
    {
        cached.unlockedEngravings = ParseList(PlayerPrefs.GetString(EngravingsUnlockedKey, string.Empty));
        return cached.unlockedEngravings;
    }

    /// <summary>
    /// 把铭文加入已解锁列表。
    /// </summary>
    public void UnlockEngraving(string engravingId)
    {
        if (string.IsNullOrEmpty(engravingId)) return;
        var list = new List<string>(GetUnlockedEngravings());
        if (list.Contains(engravingId)) return;
        list.Add(engravingId);
        PlayerPrefs.SetString(EngravingsUnlockedKey, string.Join(";", list));
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 读取当前装备的铭文列表。
    /// </summary>
    public IReadOnlyList<string> GetSelectedEngravings()
    {
        cached.selectedEngravings = ParseList(PlayerPrefs.GetString(EngravingsSelectedKey, string.Empty));
        return cached.selectedEngravings;
    }

    /// <summary>
    /// 保存当前装备的铭文列表。
    /// </summary>
    public void SetSelectedEngravings(IReadOnlyList<string> engravingIds)
    {
        PlayerPrefs.SetString(EngravingsSelectedKey,
            engravingIds == null ? string.Empty : string.Join(";", new List<string>(engravingIds)));
        PlayerPrefs.Save();
        cached.selectedEngravings = engravingIds == null
            ? new List<string>()
            : new List<string>(engravingIds);
    }

    /// <summary>
    /// 清空已装备的铭文。
    /// </summary>
    public void ClearSelectedEngravings()
    {
        PlayerPrefs.DeleteKey(EngravingsSelectedKey);
        PlayerPrefs.Save();
        cached.selectedEngravings = new List<string>();
    }

    // ---- 辅助方法 ----

    /// <summary>
    /// 广播金币变化事件。
    /// </summary>
    private void NotifyCoinsChanged(int value)
    {
        var handler = OnCoinsChanged;
        if (handler != null) handler.Invoke(value);
    }

    /// <summary>
    /// 按「加入/移除」语义同步缓存列表中的某个 id。
    /// </summary>
    private static void SyncCacheList(List<string> list, string id, bool present)
    {
        if (list == null || string.IsNullOrEmpty(id)) return;
        if (present && !list.Contains(id)) list.Add(id);
        else if (!present) list.Remove(id);
    }

    /// <summary>解析旧版 ";" 分隔的铭文 id 串，兼容空段与空白。</summary>
    private static List<string> ParseList(string raw)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(raw)) return list;
        foreach (var id in raw.Split(';'))
        {
            if (!string.IsNullOrEmpty(id)) list.Add(id);
        }
        return list;
    }
}
