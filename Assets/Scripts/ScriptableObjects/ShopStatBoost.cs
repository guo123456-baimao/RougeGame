using UnityEngine;
[CreateAssetMenu(fileName = "StatBoost", menuName = "Shop/StatBoost")]
public class ShopStatBoost : ShopItemBlueprint
{
    public StatBoostType boostType;
    public int boostAmount;

    /// <summary>
    /// 购买后把属性加成累加到本局状态。
    /// </summary>
    public override void Purchase()
    {
        RunState.AddStatBoost(boostType, boostAmount);
    }

    /// <summary>
    /// 返回属性加成的描述文案。
    /// </summary>
    public override string GetDescription()
    {
        string typeName = GetStatTypeName(boostType);
        return $"{typeName} +{boostAmount}";
    }

    /// <summary>
    /// 把属性类型枚举转换成可读名称。
    /// </summary>
    private string GetStatTypeName(StatBoostType type)
    {
        switch (type)
        {
            case StatBoostType.HP: return "生命值";
            case StatBoostType.Armor: return "护甲";
            case StatBoostType.MoveSpeed: return "移动速度";
            case StatBoostType.Luck: return "幸运";
            default: return type.ToString();
        }
    }

    public enum StatBoostType
    {
        HP,
        Armor,
        MoveSpeed,
        Luck
    }
}
