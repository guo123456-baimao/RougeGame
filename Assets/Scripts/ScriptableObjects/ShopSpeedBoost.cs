using UnityEngine;
[CreateAssetMenu(fileName = "ShopSpeedBoost", menuName = "Shop/ShopStatBoost/MoveSpeed Boost")]
public class ShopSpeedBoost : ShopStatBoost
{
    public override string GetDescription() => $"移动速度 +{boostAmount}%";
}
