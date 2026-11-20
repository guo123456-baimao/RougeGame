using UnityEngine;
public class IntUpgradeAbility<T> : Ability where T : UpgradeableInt
{
    [SerializeField] protected int[] upgrades;

    public override string Description
    {
        get {
            return GetUpgradeDescription();
        }
    }

    /// <summary>
    /// 首次获得时启用对象，并立即应用第一档数值。
    /// </summary>
    protected override void Use()
    {
        base.Use();
        gameObject.SetActive(true);
        abilityManager.UpgradeValue<T, int>(upgrades[level]);
    }

    /// <summary>
    /// 升级时把下一档数值加到对应属性上。
    /// </summary>
    protected override void Upgrade()
    {
        abilityManager.UpgradeValue<T, int>(upgrades[level]);
    }

    /// <summary>
    /// 还有未使用的升级档位时才可再次出现。
    /// </summary>
    public override bool RequirementsMet()
    {
        return level < upgrades.Length;
    }

    /// <summary>
    /// 生成本级升级数值的描述文案。
    /// </summary>
    protected string GetUpgradeDescription()
    {
        return DescriptionUtils.GetUpgradeDescription(localizedDescription, upgrades[level]);
    }
}
