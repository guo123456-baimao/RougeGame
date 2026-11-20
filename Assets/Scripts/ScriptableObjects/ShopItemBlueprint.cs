using UnityEngine;
[CreateAssetMenu(fileName = "ShopItem", menuName = "Shop/ShopItem")]
public abstract class ShopItemBlueprint : ScriptableObject
{
    public string displayName;
    public int cost;

    public virtual int GetCost() => cost;
    public Sprite icon;
    public ShopItemType itemType;

    /// <summary>
    /// 返回物品显示名（有本地化文本时优先使用）。
    /// </summary>
    public string GetDisplayName()
    {
        return displayName ?? name;
    }

    /// <summary>
    /// 生成这次购买的扣费订单：扣多少钱、写哪些永久权益，由道具自己说了算。
    /// 服务端和本地存储执行的是同一份订单，避免两处规则不一致。
    /// </summary>
    public virtual SpendOrder CreateSpendOrder()
    {
        return new SpendOrder { ItemId = name, Cost = GetCost() };
    }

    /// <summary>
    /// 只负责本局生效的加成（属性强化、发放消耗品）。
    /// 永久权益（解锁角色、金币变化）已由订单交给存储层写入，
    /// 不要在这里再写一遍，否则联网时会多发一次请求。
    /// </summary>
    public virtual void Purchase() { }
    public virtual string GetDescription() => "";

    public enum ShopItemType
    {
        StatBoost,
        Consumable
    }
}
