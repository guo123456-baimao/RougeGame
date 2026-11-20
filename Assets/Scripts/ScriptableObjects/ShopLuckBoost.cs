using UnityEngine;
[CreateAssetMenu(fileName = "ShopLuckBoost", menuName = "Shop/ShopStatBoost/Luck Boost")]
public class ShopLuckBoost : ShopStatBoost
{
    public override string GetDescription() => $"运气值 +{boostAmount}";
}
