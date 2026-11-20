using System;
/// <summary>
/// 一次消费的"订单"：描述扣多少钱、换来哪些永久权益。
///
/// P4 之前，购买是这样做的：客户端自己算余额、直接 SetCoins，改本地文件就能刷币。
/// 现在改成下订单：服务端在一个事务里完成「校验余额 → 扣费 → 写权益」，
/// 客户端只负责事后展示结果。本地离线实现走同一份订单，两条路径行为一致。
/// </summary>
public class SpendOrder
{
    /// <summary>订单标识，用商品名或 "Character:uid" / "Engraving:id" 之类。</summary>
    public string ItemId;

    public int Cost;
    public string GrantsCharacterUid;
    public string UnlocksEngraving;
    public int GrantsCoins;

    /// <summary>
    /// 构造商店道具的扣费订单。
    /// </summary>
    public static SpendOrder ForShopItem(string itemId, int cost)
    {
        return new SpendOrder { ItemId = itemId, Cost = cost };
    }

    /// <summary>
    /// 构造角色解锁的扣费订单。
    /// </summary>
    public static SpendOrder ForCharacter(string characterUid, int cost)
    {
        return new SpendOrder
        {
            ItemId = "Character:" + characterUid,
            Cost = cost,
            GrantsCharacterUid = characterUid
        };
    }

    /// <summary>
    /// 构造铭文解锁的扣费订单。
    /// </summary>
    public static SpendOrder ForEngraving(string engravingId, int cost)
    {
        return new SpendOrder
        {
            ItemId = "Engraving:" + engravingId,
            Cost = cost,
            UnlocksEngraving = engravingId
        };
    }
}

/// <summary>扣费结果。Success 为 true 时 Coins 是服务端权威余额。</summary>
public class PurchaseOutcome
{
    public bool Success;
    public int Coins;
    public string Message;

    /// <summary>
    /// 构造成功的扣费结果，附带服务端权威余额。
    /// </summary>
    public static PurchaseOutcome Ok(int coins)
    {
        return new PurchaseOutcome { Success = true, Coins = coins, Message = "ok" };
    }

    /// <summary>
    /// 构造失败的扣费结果并附带错误信息。
    /// </summary>
    public static PurchaseOutcome Fail(string message)
    {
        return new PurchaseOutcome { Success = false, Message = message };
    }
}
