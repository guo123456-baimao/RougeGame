public class CooldownUpgradeAbility : FloatUpgradeAbility<UpgradeableWeaponCooldown>
{
    /// <summary>
    /// 仅在冷却类属性仍可升级、且相关技能已装备时才会出现。
    /// </summary>
    public override bool RequirementsMet()
    {
        bool baseRequirementsMet = base.RequirementsMet();
        bool cooldownAbilitiesInUse = abilityManager.WeaponCooldownUpgradeablesCount > 0;
        return baseRequirementsMet && cooldownAbilitiesInUse;
    }
}
