using System.Collections;
using UnityEngine;
public class RangedMonster : Monster
{
    public enum State
    {
        Walking,
        Shooting
    }

    [SerializeField] protected Transform projectileSpawnPosition;
    protected float timeSinceLastAttack;
    protected State state;
    protected float outOfRangeTime;
    protected int projectileIndex;

    protected RangedMonsterBlueprint rangedBlueprint => (RangedMonsterBlueprint)monsterBlueprint;
    protected override float CurrentAttackDamage => rangedBlueprint.atk * damageMultiplier;

    /// <summary>
    /// 完成基类初始化，并注册自身弹丸池、重置射程外计时。
    /// </summary>
    public override void Setup(int monsterIndex, Vector2 position, MonsterBlueprint monsterBlueprint, float hpBuff = 0, EliteSpawnInfo eliteInfo = default(EliteSpawnInfo))
    {
        base.Setup(monsterIndex, position, monsterBlueprint, hpBuff, eliteInfo);
        projectileIndex = entityManager.AddPoolForProjectile(rangedBlueprint.projectilePrefab);
        outOfRangeTime = 0;
    }

    /// <summary>
    /// 状态机驱动：追击玩家；进入射程后停下并按攻速开火，长时间脱离射程则恢复追击。
    /// </summary>
    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        if (alive)
        {
            Vector2 toPlayer = (playerCharacter.transform.position - transform.position);
            float distance = toPlayer.magnitude;
            Vector2 dirToPlayer = toPlayer/distance;
            switch (state)
            {
                case State.Walking:
                    rb.velocity += dirToPlayer * rangedBlueprint.acceleration * Time.fixedDeltaTime;
                    entityManager.Grid.UpdateClient(this);
                    if (distance <= rangedBlueprint.range)
                    {
                        state = State.Shooting;
                        monsterSpriteAnimator.StopAnimating();
                        //rb.bodyType = RigidbodyType2D.Static;
                        //rb.mass = 9999;
                    }
                    break;

                case State.Shooting:
                    timeSinceLastAttack += Time.fixedDeltaTime;
                    // rb.velocity *= 0.95f;
                    if (timeSinceLastAttack >= 1.0f/(rangedBlueprint.atkspeed * attackSpeedMultiplier))
                    {
                        LaunchProjectile(dirToPlayer);
                        timeSinceLastAttack = 0;//Mathf.Repeat(timeSinceLastAttack, 1.0f/rangedBlueprint.atkspeed);
                    }
                    if (distance <= rangedBlueprint.range)
                        outOfRangeTime = 0;
                    else
                        outOfRangeTime += Time.deltaTime;
                    if (outOfRangeTime > rangedBlueprint.timeAllowedOutsideRange)
                    {
                        state = State.Walking;
                        monsterSpriteAnimator.StartAnimating();
                        //rb.bodyType = RigidbodyType2D.Dynamic;
                        //rb.mass = 1;
                    }
                    break;
            }
            // if (!knockedBack && rb.velocity.magnitude > monsterBlueprint.movespeed)
            //      rb.velocity = rb.velocity.normalized * monsterBlueprint.movespeed;
        }
    }

    /// <summary>
    /// 从枪口位置生成一枚弹丸并朝目标方向发射。
    /// </summary>
    protected void LaunchProjectile(Vector2 direction)
    {
        Projectile projectile = entityManager.SpawnProjectile(projectileIndex, projectileSpawnPosition.position, rangedBlueprint.atk * damageMultiplier, 0, rangedBlueprint.projectileSpeed, rangedBlueprint.targetLayer);
        projectile.Launch(direction);
    }
}
