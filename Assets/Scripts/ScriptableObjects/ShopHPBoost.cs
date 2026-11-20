using UnityEngine;
[CreateAssetMenu(fileName = "ShopHPBoost", menuName = "Shop/ShopStatBoost/HP Boost")]
public class ShopHPBoost : ShopStatBoost
{
    public override string GetDescription() => $"生命值上限 +{boostAmount}";
}
