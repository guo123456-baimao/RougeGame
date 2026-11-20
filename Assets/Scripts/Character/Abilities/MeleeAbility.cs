using UnityEngine;
public abstract class MeleeAbility : Ability
{
    [Header("Melee Stats")]
    [SerializeField] protected LayerMask targetLayer;
    [SerializeField] protected UpgradeableDamage damage;
    [SerializeField] protected UpgradeableKnockback knockback;
    [SerializeField] protected UpgradeableWeaponCooldown cooldown;
    [SerializeField] protected SpriteRenderer weaponSpriteRenderer;
    protected float timeSinceLastAttack;

    public override bool HasCooldown { get => true; }
    public override float CooldownProgress
    {
        get => cooldown.Value > 0f ? Mathf.Clamp01(timeSinceLastAttack / cooldown.Value) : 1f;
    }

    /// <summary>
    /// 启用近战武器并开始挥击循环。
    /// </summary>
    protected override void Use()
    {
        base.Use();
        gameObject.SetActive(true);
        timeSinceLastAttack = cooldown.Value;
    }

    /// <summary>
    /// 冷却结束后自动发起一次挥击。
    /// </summary>
    void Update()
    {
        timeSinceLastAttack += Time.deltaTime;
        if (timeSinceLastAttack >= cooldown.Value)
        {
            timeSinceLastAttack = Mathf.Repeat(timeSinceLastAttack, cooldown.Value);
            Attack();
        }
    }

    protected abstract void Attack();

    /// <summary>
    /// 挥击命中判定时的回调，供子类扩展。
    /// </summary>
    protected virtual void OnAttackHit()
    {
        AudioManager.Instance.PlayAxeAttack();
    }
}
