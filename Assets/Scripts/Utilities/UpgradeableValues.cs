using UnityEngine;
using UnityEngine.Events;
public interface IUpgradeableValue
{
    int UpgradeCount { get; }

    void Register(AbilityManager abilityManager);
    void RegisterInUse();
    void Upgrade();
    string GetUpgradeDescription();
}

public abstract class UpgradeableValue<T> : IUpgradeableValue
{
    [SerializeField] protected T value;
    [SerializeField] protected T[] upgrades;
    protected AbilityManager abilityManager;
    protected int level = 0;

    protected virtual string UpgradeName { get; set; }
    public virtual T Value { get => value; set => this.value = value; }
    public int UpgradeCount { get => upgrades.Length; }
    public UnityEvent OnChanged { get; } = new UnityEvent();

    public void Register(AbilityManager abilityManager) { this.abilityManager = abilityManager; }
    public abstract void RegisterInUse();
    /// <summary>
    /// 升级入口，由具体数值类型重写。
    /// </summary>
    public virtual void Upgrade()
    {
        if (level < upgrades.Length)
            Upgrade(upgrades[level++]);
    }
    public abstract void Upgrade(T upgrade);
    public abstract string GetUpgradeDescription();
}

////////////////////////////////////////////////////////////////////////////////
/// 可升级的浮点数
////////////////////////////////////////////////////////////////////////////////
public abstract class UpgradeableFloat : UpgradeableValue<float>
{
    /// <summary>
    /// 把浮点数值提升指定量。
    /// </summary>
    public override void Upgrade(float upgrade)
    {
        value *= (1+upgrade);
        OnChanged.Invoke();
    }

    /// <summary>
    /// 生成按百分比提升的升级描述，数据用尽或增量为 0 时返回空串。
    /// </summary>
    public override string GetUpgradeDescription()
    {
        if (level >= upgrades.Length || upgrades[level] == 0) return "";
        return DescriptionUtils.GetUpgradeDescription(UpgradeName, upgrades[level]);
    }
}

[System.Serializable] public class UpgradeableDamage : UpgradeableFloat
{
    protected override string UpgradeName => "伤害";
    /// <summary>
    /// 登记「伤害」属性已被技能使用，供抽卡条件判断。
    /// </summary>
    public override void RegisterInUse() { abilityManager.DamageUpgradeablesCount++; }

    // 铭文等来源的全局伤害倍率在读取时统一乘上，各 Ability 不用逐个处理。
    // 升级走 Upgrade(float)，直接改 value 字段，不会把倍率乘进基础值。
    public override float Value
    {
        get => value * EngravingManager.DamageMultiplier;
        set => this.value = value;
    }
}
[System.Serializable] public class UpgradeableDamageRate : UpgradeableFloat
{
    protected override string UpgradeName => "伤害率";
    /// <summary>
    /// 登记「射速」属性已被技能使用。
    /// </summary>
    public override void RegisterInUse() { abilityManager.FireRateUpgradeablesCount++; }
}
[System.Serializable] public class UpgradeableWeaponCooldown : UpgradeableFloat
{
    protected override string UpgradeName => "武器冷却";
    /// <summary>
    /// 登记「武器冷却」属性已被技能使用。
    /// </summary>
    public override void RegisterInUse() { abilityManager.WeaponCooldownUpgradeablesCount++; }
}
[System.Serializable] public class UpgradeableRecoveryCooldown : UpgradeableFloat
{
    protected override string UpgradeName => "恢复冷却";
    /// <summary>
    /// 登记「恢复冷却」属性已被技能使用。
    /// </summary>
    public override void RegisterInUse() { abilityManager.RecoveryCooldownUpgradeablesCount++; }
}
[System.Serializable] public class UpgradeableDuration : UpgradeableFloat
{
    protected override string UpgradeName => "持续时间";
    /// <summary>
    /// 登记「持续时间」属性已被技能使用。
    /// </summary>
    public override void RegisterInUse() { abilityManager.DurationUpgradeablesCount++; }
}
[System.Serializable] public class UpgradeableAOE : UpgradeableFloat
{
    protected override string UpgradeName => "范围";
    /// <summary>
    /// 登记「范围」属性已被技能使用。
    /// </summary>
    public override void RegisterInUse() { abilityManager.AOEUpgradeablesCount++; }
}
[System.Serializable] public class UpgradeableKnockback : UpgradeableFloat
{
    protected override string UpgradeName => "击退";
    /// <summary>
    /// 登记「击退」属性已被技能使用。
    /// </summary>
    public override void RegisterInUse() { abilityManager.KnockbackUpgradeablesCount++; }
}
[System.Serializable] public class UpgradeableProjectileSpeed : UpgradeableFloat
{
    protected override string UpgradeName => "投射物速度";
    /// <summary>
    /// 登记「弹丸速度」属性已被技能使用。
    /// </summary>
    public override void RegisterInUse() { abilityManager.ProjectileSpeedUpgradeablesCount++; }
}
[System.Serializable] public class UpgradeableRecoveryChance : UpgradeableFloat
{
    protected override string UpgradeName => "恢复几率";
    /// <summary>
    /// 登记「恢复概率」属性已被技能使用。
    /// </summary>
    public override void RegisterInUse() { abilityManager.RecoveryChanceUpgradeablesCount++; }
}
[System.Serializable] public class UpgradeableBleedDamage : UpgradeableFloat
{
    protected override string UpgradeName => "流血伤害";
    /// <summary>
    /// 登记「流血伤害」属性已被技能使用。
    /// </summary>
    public override void RegisterInUse() { abilityManager.BleedDamageUpgradeablesCount++; }

    public override float Value
    {
        get => value * EngravingManager.DamageMultiplier;
        set => this.value = value;
    }
}
[System.Serializable] public class UpgradeableBleedRate : UpgradeableFloat
{
    protected override string UpgradeName => "流血速率";
    /// <summary>
    /// 登记「流血频率」属性已被技能使用。
    /// </summary>
    public override void RegisterInUse() { abilityManager.BleedRateUpgradeablesCount++; }
}
[System.Serializable] public class UpgradeableBleedDuration : UpgradeableFloat
{
    protected override string UpgradeName => "流血时长";
    /// <summary>
    /// 登记「流血持续时间」属性已被技能使用。
    /// </summary>
    public override void RegisterInUse() { abilityManager.BleedDurationUpgradeablesCount++; }
}
[System.Serializable] public class UpgradeableMovementSpeed : UpgradeableFloat
{
    protected override string UpgradeName => "移动速度";
    /// <summary>
    /// 登记「移动速度」属性已被技能使用。
    /// </summary>
    public override void RegisterInUse() { abilityManager.MovementSpeedUpgradeablesCount++; }
}
[System.Serializable] public class UpgradeableRotationSpeed : UpgradeableFloat
{
    protected override string UpgradeName => "旋转速度";
    /// <summary>
    /// 登记「环绕速度」属性已被技能使用。
    /// </summary>
    public override void RegisterInUse() { abilityManager.RotationSpeedUpgradeablesCount++; }
}

////////////////////////////////////////////////////////////////////////////////
/// 可升级的整数
////////////////////////////////////////////////////////////////////////////////
public abstract class UpgradeableInt : UpgradeableValue<int>
{
    /// <summary>
    /// 把整数数值提升指定量。
    /// </summary>
    public override void Upgrade(int upgrade)
    {
        value += upgrade;
        OnChanged.Invoke();
    }

    /// <summary>
    /// 生成按固定值提升的升级描述，数据用尽或增量为 0 时返回空串。
    /// </summary>
    public override string GetUpgradeDescription()
    {
        if (level >= upgrades.Length || upgrades[level] == 0) return "";
        return DescriptionUtils.GetUpgradeDescription(UpgradeName, upgrades[level]);
    }
}

[System.Serializable] public class UpgradeableProjectileCount : UpgradeableInt
{
    [SerializeField] protected int projectilesPer = 1;
    public override int Value { get => projectilesPer * value; }
    protected override string UpgradeName => "投射物数量";
    /// <summary>
    /// 登记「弹丸数量」属性已被技能使用。
    /// </summary>
    public override void RegisterInUse() { abilityManager.ProjectileCountUpgradeablesCount++; }
    /// <summary>
    /// 生成投射物数量的升级描述（数值按每轮投射量换算）。
    /// </summary>
    public override string GetUpgradeDescription()
    {
        if (level >= upgrades.Length || upgrades[level] == 0) return "";
        return DescriptionUtils.GetUpgradeDescription(UpgradeName, upgrades[level]*projectilesPer);
    }
}
[System.Serializable] public class UpgradeableRecovery : UpgradeableInt
{
    protected override string UpgradeName => "恢复";
    /// <summary>
    /// 登记「恢复量」属性已被技能使用。
    /// </summary>
    public override void RegisterInUse() { abilityManager.RecoveryUpgradeablesCount++; }
}
[System.Serializable] public class UpgradeableArmor : UpgradeableInt
{
    protected override string UpgradeName => "护甲";
    /// <summary>
    /// 登记「护甲」属性已被技能使用。
    /// </summary>
    public override void RegisterInUse() { abilityManager.ArmorUpgradeablesCount++; }
}
