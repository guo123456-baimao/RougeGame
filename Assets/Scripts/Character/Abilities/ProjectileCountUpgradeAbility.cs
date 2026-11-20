public class ProjectileCountUpgradeAbility : IntUpgradeAbility<UpgradeableProjectileCount>
{
    /// <summary>
    /// 仅在弹丸数量仍可升级、且相关技能已装备时才会出现。
    /// </summary>
    public override bool RequirementsMet()
    {
        bool baseRequirementsMet = base.RequirementsMet();
        bool projectileAbilitiesInUse = abilityManager.ProjectileCountUpgradeablesCount > 0;
        return baseRequirementsMet && projectileAbilitiesInUse;
    }
}
