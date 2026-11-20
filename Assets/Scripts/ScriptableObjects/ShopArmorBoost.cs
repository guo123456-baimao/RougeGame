using UnityEngine;
[CreateAssetMenu(fileName = "ShopArmorBoost", menuName = "Shop/ShopStatBoost/Armor Boost")]
public class ShopArmorBoost : ShopStatBoost
{
    public override string GetDescription() => $"护甲值 +{boostAmount}";
}
