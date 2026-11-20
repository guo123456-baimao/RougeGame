using UnityEngine;
[CreateAssetMenu(fileName = "CoinBuyback", menuName = "Shop/CoinBuyback")]
public class CoinBuyback : ShopItemBlueprint
{
    public int coinAmount;

    /// <summary>回购金币：净额 = 返还金币 - 花费，交给服务端在一个事务里算。</summary>
    public override SpendOrder CreateSpendOrder()
    {
        return new SpendOrder
        {
            ItemId = name,
            Cost = GetCost(),
            GrantsCoins = coinAmount
        };
    }

    /// <summary>
    /// 金币变化由订单处理
    /// </summary>
    public override void Purchase() { }

    /// <summary>
    /// 返回消耗品描述文案（含本局已购买数量）。
    /// </summary>
    public override string GetDescription() => $"获得 {coinAmount} 金币";
}

[CreateAssetMenu(fileName = "ConsumableBundle", menuName = "Shop/ConsumableBundle")]
public class ConsumableBundle : ShopItemBlueprint
{
    [SerializeField] public CollectableType[] consumables;
    [SerializeField] public int[] amounts;

    /// <summary>
    /// 购买后把消耗品记入单局待投放列表，已进入局内时立即投放。
    /// </summary>
    public override void Purchase()
    {
        // 消耗品条目记入单局内存状态（类型资产名做 key，跨会话稳定），等进入游戏场景后由 EntityManager 投放
        for (int i = 0; i < consumables.Length; i++)
        {
            if (consumables[i] == null) continue;
            RunState.AddPurchasedConsumable(consumables[i].name, amounts[i]);
        }

        EntityManager em = CrossSceneData.EntityManager;
        if (em != null)
        {
            em.SpawnCollectablesFromSession(em.PlayerPosition);
        }
    }

    /// <summary>
    /// 拼接消耗品礼包的描述文案（各项名称与数量）。
    /// </summary>
    public override string GetDescription()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < consumables.Length; i++)
        {
            if (i > 0) sb.Append(" + ");
            sb.Append(consumables[i].name).Append(" x").Append(amounts[i]);
        }
        return sb.ToString();
    }
}
