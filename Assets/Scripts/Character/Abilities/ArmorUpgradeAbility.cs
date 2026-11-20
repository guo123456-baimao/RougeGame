public class ArmorUpgradeAbility : IntUpgradeAbility<UpgradeableArmor>
{
    /// <summary>
    /// 仅在护甲仍可升级、且相关技能已装备时才会出现。
    /// </summary>
    public override bool RequirementsMet()
    {
        bool baseRequirementsMet = base.RequirementsMet();
        bool armorInUse = abilityManager.ArmorUpgradeablesCount > 0;
        return baseRequirementsMet && armorInUse;
    }
}
