public class DamageUpgradeAbility : FloatUpgradeAbility<UpgradeableDamage>
{
    /// <summary>
    /// 仅在伤害类属性仍可升级、且场上已有可升级伤害的技能时才会出现。
    /// </summary>
    public override bool RequirementsMet()
    {
        bool baseRequirementsMet = base.RequirementsMet();
        bool damageAbilitiesInUse = abilityManager.DamageUpgradeablesCount > 0;
        return baseRequirementsMet && damageAbilitiesInUse;
    }
}
