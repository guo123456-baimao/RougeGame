public class AOEUpgradeAbility : FloatUpgradeAbility<UpgradeableAOE>
{
    /// <summary>
    /// 仅在范围类属性仍可升级、且场上已有依赖范围的技能时才会出现。
    /// </summary>
    public override bool RequirementsMet()
    {
        bool baseRequirementsMet = base.RequirementsMet();
        bool aoeAbilitiesInUse = abilityManager.AOEUpgradeablesCount > 0;
        return baseRequirementsMet && aoeAbilitiesInUse;
    }
}
