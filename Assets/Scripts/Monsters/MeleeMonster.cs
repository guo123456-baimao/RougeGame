using System.Collections;
using UnityEngine;
public class MeleeMonster : Monster
{
    protected float timeSinceLastAttack;
    protected MeleeMonsterBlueprint meleeBP => (MeleeMonsterBlueprint)monsterBlueprint;
    protected override float CurrentAttackDamage => meleeBP.atk * damageMultiplier;

    /// <summary>
    /// 怪物出场初始化，沿用基类逻辑（近战怪无额外初始化）。
    /// </summary>
    public override void Setup(int monsterIndex, Vector2 position, MonsterBlueprint monsterBlueprint, float hpBuff = 0, EliteSpawnInfo eliteInfo = default(EliteSpawnInfo))
    {
        base.Setup(monsterIndex, position, monsterBlueprint, hpBuff, eliteInfo);
    }

    /// <summary>
    /// 每帧累加攻击冷却计时。
    /// </summary>
    protected override void Update()
    {
        base.Update();
        // 攻击冷却
        timeSinceLastAttack += Time.deltaTime;
    }

    /// <summary>
    /// 朝玩家持续加速移动，并同步自身在空间网格中的位置。
    /// </summary>
    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        Vector2 moveDirection = (playerCharacter.transform.position - transform.position).normalized;
        rb.velocity += moveDirection * meleeBP.acceleration * Time.fixedDeltaTime;
        entityManager.Grid.UpdateClient(this);

        // Vector2 f = Vector2.zero;
        // int count = 0;
        // foreach (ISpatialHashGridClient client in entityManager.Grid.FindNearbyInRadius(Position, 2))
        // {
        //     if (client != (ISpatialHashGridClient)this)
        //     {
        //         Vector2 diff = Position - client.Position;
        //         float dist = diff.magnitude;
        //         diff /= dist;
        //         f += diff / dist;
        //         count++;
        //     }
        // }
        // if (count > 0)
        //     f /= count;
        // rb.velocity += f * 0.1f * meleeBP.acceleration * Time.fixedDeltaTime;
        // if (!knockedBack && rb.velocity.magnitude > meleeBP.movespeed)
        //      rb.velocity = rb.velocity.normalized * meleeBP.movespeed;
    }

    /// <summary>
    /// 持续接触玩家且冷却就绪时，按攻速结算一次近战伤害。
    /// </summary>
    void OnCollisionStay2D(Collision2D col)
    {
        float attackInterval = 1.0f/(meleeBP.atkspeed * attackSpeedMultiplier);
        if (alive && ((meleeBP.meleeLayer & (1 << col.collider.gameObject.layer)) != 0) && timeSinceLastAttack >= attackInterval)
        {
            playerCharacter.TakeDamage(meleeBP.atk * damageMultiplier);
            timeSinceLastAttack = Mathf.Repeat(timeSinceLastAttack, attackInterval);
        }
    }
}
