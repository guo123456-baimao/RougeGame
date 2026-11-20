using System.Collections;
using UnityEngine;
public class GravityWellAbility : ThrowableAbility
{
    [Header("Gravity Well Stats")]
    [SerializeField] protected UpgradeableDuration duration;
    [SerializeField] protected UpgradeableAOE wellRadius;

    /// <summary>
    /// 在玩家附近生成引力井，周期性吸附并伤害敌人。
    /// </summary>
    protected override void Use()
    {
        base.Use();
        AudioManager.Instance.PlayGravityWell();
    }
}
