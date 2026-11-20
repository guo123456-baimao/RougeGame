using System;
using System.Collections.Generic;
/// <summary>
/// 存储抽象层：所有"永久玩家数据"的读写都必须经过这里，禁止在业务代码里直接调 PlayerPrefs。
/// P1：PlayerPrefsDataStore（本地，行为与旧版一致）。
/// P3：ApiDataStore（联网，走 ASP.NET Core + MySQL），切换只改 DataStoreLocator 一行。
/// </summary>
public interface IDataStore
{
    /// <summary>当前缓存的玩家档案。同步读、不阻塞，供 UI 频繁刷新使用。</summary>
    PlayerProfile CachedProfile { get; }

    /// <summary>当前实现是否联网（本地存储 = false，数据库实现 = true）。</summary>
    bool IsOnline { get; }

    /// <summary>金币变化后触发，参数为最新金币数。UI 应订阅它，而不是每帧直读存储。</summary>
    event Action<int> OnCoinsChanged;

    // ---- 金币（永久） ----
    int GetCoins();
    void SetCoins(int value);

    /// <summary>增减金币，delta 可为负；结果不会低于 0。</summary>
    void AddCoins(int delta);

    // ---- 消费（P4：服务端权威） ----

    /// <summary>
    /// 按订单扣费。联网实现会在服务端事务里完成余额校验与权益写入，
    /// 本地实现按同样规则逐条落到 PlayerPrefs，两条路径语义一致。
    /// 结果通过回调返回（可能是异步的），调用方据此决定是否生效局部加成。
    /// </summary>
    void SpendCoins(SpendOrder order, Action<PurchaseOutcome> onResult);

    /// <summary>单局结束结算奖励金币。联网实现由服务端加息，防止客户端谎报战果。</summary>
    void SettleRunReward(int coinsGained);

    // ---- 角色拥有（永久） ----
    bool IsCharacterOwned(string characterUid);
    void SetCharacterOwned(string characterUid, bool owned);

    // ---- 本局进度（未结算，跟着账号走） ----

    /// <summary>
    /// 取出本局进度快照（JSON 串）：本局已购商品名 + 已生效加成 + 待投放消耗品。
    ///
    /// 金币是从账号里扣的，买到的东西就必须记在账号上——只记在内存里会出现
    /// 「退出登录再登回来，东西没了；换个号反而还在」这种串号。
    /// 一局结束（死亡 / 通关 / 返回首页）时由 ShopSession.Clear 传空串清空。
    /// </summary>
    string GetSessionSnapshot();

    /// <summary>写入本局进度快照；传 null 或空串表示清空（本局已结束）。</summary>
    void SetSessionSnapshot(string snapshot);

    // ---- 铭文（永久） ----
    IReadOnlyList<string> GetUnlockedEngravings();
    void UnlockEngraving(string engravingId);
    IReadOnlyList<string> GetSelectedEngravings();

    /// <summary>整体覆写装备列表（最多 MaxSelectedSlots 个，由调用方保证）。</summary>
    void SetSelectedEngravings(IReadOnlyList<string> engravingIds);

    /// <summary>清空装备列表（对应原 PlayerPrefs.DeleteKey(Engravings_Selected)）。</summary>
    void ClearSelectedEngravings();
}
