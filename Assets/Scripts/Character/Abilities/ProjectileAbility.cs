using System.Collections;
using UnityEngine;
public class ProjectileAbility : Ability
{
    [Header("Projectile Stats")]
    [SerializeField] protected GameObject projectilePrefab;
    [SerializeField] protected LayerMask monsterLayer;
    [SerializeField] protected UpgradeableDamage damage;
    [SerializeField] protected UpgradeableProjectileSpeed speed;
    [SerializeField] protected UpgradeableKnockback knockback;
    [SerializeField] protected UpgradeableWeaponCooldown cooldown;
    protected float timeSinceLastAttack;
    protected int projectileIndex;

    public override bool HasCooldown { get => true; }
    public override float CooldownProgress
    {
        get => cooldown.Value > 0f ? Mathf.Clamp01(timeSinceLastAttack / cooldown.Value) : 1f;
    }

    /// <summary>
    /// 启用武器并开始射击循环。
    /// </summary>
    protected override void Use()
    {
        base.Use();
        gameObject.SetActive(true);
        timeSinceLastAttack = cooldown.Value;
        projectileIndex = entityManager.AddPoolForProjectile(projectilePrefab);
    }

    /// <summary>
    /// 按冷却自动开火。
    /// </summary>
    protected virtual void Update()
    {
        timeSinceLastAttack += Time.deltaTime;
        if (timeSinceLastAttack >= cooldown.Value)
        {
            timeSinceLastAttack = Mathf.Repeat(timeSinceLastAttack, cooldown.Value);
            Attack();
        }
    }

    /// <summary>
    /// 启动一轮攻击，由子类决定具体表现。
    /// </summary>
    protected virtual void Attack()
    {
        LaunchProjectile();
    }

    /// <summary>
    /// 发射一枚弹丸，具体参数由子类实现。
    /// </summary>
    protected virtual void LaunchProjectile()
    {
        Projectile projectile = entityManager.SpawnProjectile(projectileIndex, playerCharacter.CenterTransform.position, damage.Value, knockback.Value, speed.Value, monsterLayer);
        projectile.OnHitDamageable.AddListener(playerCharacter.OnDealDamage.Invoke);
        projectile.Launch(FindNearestEnemyDirection());
    }
}
