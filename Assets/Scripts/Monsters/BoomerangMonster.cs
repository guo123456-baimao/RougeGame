using System.Collections;
using UnityEngine;
public class BoomerangMonster : Monster
{
    [SerializeField] protected Transform boomerangSpawnPosition;
    protected float timeSinceLastBoomerangAttack;
    protected float timeSinceLastMeleeAttack;
    protected float outOfRangeTime;
    protected int boomerangIndex;

    protected BoomerangMonsterBlueprint boomBlueprint => (BoomerangMonsterBlueprint)monsterBlueprint;
    protected override float CurrentAttackDamage => boomBlueprint.atk * damageMultiplier;

    public override void Setup(int monsterIndex, Vector2 position, MonsterBlueprint monsterBlueprint, float hpBuff = 0, EliteSpawnInfo eliteInfo = default(EliteSpawnInfo))
    {
        base.Setup(monsterIndex, position, monsterBlueprint, hpBuff, eliteInfo);
        boomerangIndex = entityManager.AddPoolForBoomerang(boomBlueprint.boomerangPrefab);
        outOfRangeTime = 0;
    }

    /// <summary>
    /// 状态机驱动：追击玩家，进入射程后站定并按攻速投掷回旋镖。
    /// </summary>
    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        Vector2 toPlayer = (playerCharacter.transform.position - transform.position);
        float distance = toPlayer.magnitude;
        Vector2 dirToPlayer = toPlayer/distance;
        entityManager.Grid.UpdateClient(this);
        timeSinceLastBoomerangAttack += Time.fixedDeltaTime;
        if (distance <= boomBlueprint.range)
        {
            rb.velocity += dirToPlayer * boomBlueprint.acceleration * Time.fixedDeltaTime / 2;
            if (timeSinceLastBoomerangAttack >= 1.0f/(boomBlueprint.boomerangAttackSpeed * attackSpeedMultiplier))
            {
                ThrowBoomerang(playerCharacter.transform.position);
                timeSinceLastBoomerangAttack = 0;
            }
        }
        else
        {
            rb.velocity += dirToPlayer * boomBlueprint.acceleration * Time.fixedDeltaTime;
        }
    }

    /// <summary>
    /// 按玩家预判位置投出一枚回旋镖。
    /// </summary>
    protected void ThrowBoomerang(Vector2 targetPosition)
    {
        Boomerang boomerang = entityManager.SpawnBoomerang(boomerangIndex, boomerangSpawnPosition.position, boomBlueprint.boomerangDamage * damageMultiplier, 0, boomBlueprint.throwRange, boomBlueprint.throwTime, boomBlueprint.targetLayer);
        boomerang.Throw(boomerangSpawnPosition, targetPosition);
    }

    /// <summary>
    /// 接触玩家时结算近战伤害。
    /// </summary>
    void OnCollisionStay2D(Collision2D col)
    {
        if (((boomBlueprint.targetLayer & (1 << col.collider.gameObject.layer)) != 0) && timeSinceLastMeleeAttack >= 1.0f/(boomBlueprint.atkspeed * attackSpeedMultiplier))
        {
            playerCharacter.TakeDamage(boomBlueprint.atk * damageMultiplier);
            timeSinceLastMeleeAttack = 0;
        }
    }
}
