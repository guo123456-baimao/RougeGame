public class ProjectileSpeedAbilityUpgrade : FloatUpgradeAbility<UpgradeableProjectileSpeed>
{
    /// <summary>
    /// 仅在弹丸速度仍可升级、且相关技能已装备时才会出现。
    /// </summary>
    public override bool RequirementsMet()
    {
        bool baseRequirementsMet = base.RequirementsMet();
        bool projectileAbilitiesInUse = abilityManager.ProjectileSpeedUpgradeablesCount > 0;
        return baseRequirementsMet && projectileAbilitiesInUse;
    }
}
