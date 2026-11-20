using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class BoomerangAbility : Ability
{
    [Header("Boomerang Stats")]
    [SerializeField] protected GameObject boomerangPrefab;
    [SerializeField] protected LayerMask monsterLayer;
    [SerializeField] protected float throwRadius;
    [SerializeField] protected float throwTime = 1;
    [SerializeField] protected UpgradeableDamageRate throwRate;
    [SerializeField] protected UpgradeableDamage damage;
    [SerializeField] protected UpgradeableKnockback knockback;
    [SerializeField] protected UpgradeableWeaponCooldown cooldown;
    [SerializeField] protected UpgradeableProjectileCount boomerangCount;
    protected float timeSinceLastAttack;
    protected int boomerangIndex;

    public override bool HasCooldown { get => true; }
    public override float CooldownProgress
    {
        get => cooldown.Value > 0f ? Mathf.Clamp01(timeSinceLastAttack / cooldown.Value) : 1f;
    }

    /// <summary>
    /// 启动投掷回旋镖的循环。
    /// </summary>
    protected override void Use()
    {
        base.Use();
        gameObject.SetActive(true);
        timeSinceLastAttack = cooldown.Value;
        boomerangIndex = entityManager.AddPoolForBoomerang(boomerangPrefab);
    }

    /// <summary>
    /// 冷却结束后自动投出一次回旋镖。
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
    /// 按冷却循环投出回旋镖。
    /// </summary>
    protected virtual IEnumerator Attack()
    {
        timeSinceLastAttack -= boomerangCount.Value/throwRate.Value;
        for (int i = 0; i < boomerangCount.Value; i++)
        {
            ThrowBoomerang();
            yield return new WaitForSeconds(1/throwRate.Value);
        }
    }

    /// <summary>
    /// 朝最近敌人方向投出一枚回旋镖。
    /// </summary>
    protected virtual void ThrowBoomerang()
    {
        Boomerang boomerang = entityManager.SpawnBoomerang(boomerangIndex, playerCharacter.CenterTransform.position, damage.Value, knockback.Value, throwRadius, throwTime, monsterLayer);
        Vector2 throwPosition;
        List<ISpatialHashGridClient> nearbyEnemies = entityManager.Grid.FindNearbyInRadius(playerCharacter.transform.position, throwRadius);
        if (nearbyEnemies.Count > 0)
        {
            Vector2 closestPos = nearbyEnemies[0].Position;
            float minDist = Vector2.Distance(closestPos, (Vector2)playerCharacter.transform.position);
            foreach (var enemy in nearbyEnemies)
            {
                float d = Vector2.Distance(enemy.Position, (Vector2)playerCharacter.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    closestPos = enemy.Position;
                }
            }
            throwPosition = closestPos;
        }
        else
        {
            throwPosition = (Vector2)playerCharacter.transform.position + Random.insideUnitCircle.normalized * throwRadius;
        }
        boomerang.Throw(playerCharacter.transform, throwPosition);
        AudioManager.Instance.PlayThrust();
        boomerang.OnHitDamageable.AddListener(playerCharacter.OnDealDamage.Invoke);
    }
}
