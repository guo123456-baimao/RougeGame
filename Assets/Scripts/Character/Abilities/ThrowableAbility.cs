using System.Collections;
using UnityEngine;
public class ThrowableAbility : Ability
{
    [Header("Throwable Stats")]
    [SerializeField] protected GameObject throwablePrefab;
    [SerializeField] protected LayerMask monsterLayer;
    [SerializeField] protected float throwRadius;
    [SerializeField] protected UpgradeableDamageRate throwRate;
    [SerializeField] protected UpgradeableDamage damage;
    [SerializeField] protected UpgradeableKnockback knockback;
    [SerializeField] protected UpgradeableWeaponCooldown cooldown;
    [SerializeField] protected UpgradeableProjectileCount throwableCount;
    protected float timeSinceLastAttack;
    protected int throwableIndex;

    public override bool HasCooldown { get => true; }
    public override float CooldownProgress
    {
        get => cooldown.Value > 0f ? Mathf.Clamp01(timeSinceLastAttack / cooldown.Value) : 1f;
    }

    /// <summary>
    /// 启动投掷循环。
    /// </summary>
    protected override void Use()
    {
        base.Use();
        gameObject.SetActive(true);
        timeSinceLastAttack = cooldown.Value;
        throwableIndex = entityManager.AddPoolForThrowable(throwablePrefab);
    }

    /// <summary>
    /// 冷却结束后自动投出一次投掷物。
    /// </summary>
    void Update()
    {
        timeSinceLastAttack += Time.deltaTime;
        if (timeSinceLastAttack >= cooldown.Value)
        {
            timeSinceLastAttack = Mathf.Repeat(timeSinceLastAttack, cooldown.Value);
            StartCoroutine(Attack());
        }
    }

    /// <summary>
    /// 按冷却投出一次投掷物。
    /// </summary>
    protected virtual IEnumerator Attack()
    {
        timeSinceLastAttack -= throwableCount.Value/throwRate.Value;
        for (int i = 0; i < throwableCount.Value; i++)
        {
            LaunchThrowable();
            yield return new WaitForSeconds(1/throwRate.Value);
        }
    }

    /// <summary>
    /// 生成并投出投掷物，由子类决定投掷目标。
    /// </summary>
    protected virtual void LaunchThrowable()
    {
        Throwable throwable = entityManager.SpawnThrowable(throwableIndex, playerCharacter.CenterTransform.position, damage.Value, knockback.Value, 0, monsterLayer);
        throwable.Throw((Vector2)playerCharacter.transform.position + FindNearestEnemyDirection() * throwRadius);
        throwable.OnHitDamageable.AddListener(playerCharacter.OnDealDamage.Invoke);
    }
}
