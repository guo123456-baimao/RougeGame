using System.Collections;
using UnityEngine;
public class ThrowingMonster : Monster
{
    public enum State
    {
        Walking,
        Shooting
    }

    [SerializeField] protected Transform throwableSpawnPosition;
    protected float timeSinceLastAttack;
    protected State state;
    protected float outOfRangeTime;
    protected int throwableIndex;

    protected ThrowingMonsterBlueprint throwBlueprint => (ThrowingMonsterBlueprint)monsterBlueprint;
    protected override float CurrentAttackDamage => throwBlueprint.atk * damageMultiplier;

    /// <summary>
    /// 完成基类初始化，并注册自身投掷物池、重置射程外计时。
    /// </summary>
    public override void Setup(int monsterIndex, Vector2 position, MonsterBlueprint monsterBlueprint, float hpBuff = 0, EliteSpawnInfo eliteInfo = default(EliteSpawnInfo))
    {
        base.Setup(monsterIndex, position, monsterBlueprint, hpBuff, eliteInfo);
        throwableIndex = entityManager.AddPoolForThrowable(throwBlueprint.throwablePrefab);
        outOfRangeTime = 0;
    }

    /// <summary>
    /// 状态机驱动：追击玩家；进入射程后站定按攻速投掷，长时间脱离射程则恢复追击。
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
                    rb.velocity += dirToPlayer * throwBlueprint.acceleration * Time.fixedDeltaTime;
                    if (distance <= throwBlueprint.range)
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
                    if (timeSinceLastAttack >= 1.0f/(throwBlueprint.atkspeed * attackSpeedMultiplier))
                    {
                        LaunchThrowable(playerCharacter.transform.position);
                        timeSinceLastAttack = 0;
                    }
                    if (distance <= throwBlueprint.range)
                        outOfRangeTime = 0;
                    else
                        outOfRangeTime += Time.deltaTime;
                    if (outOfRangeTime > throwBlueprint.timeAllowedOutsideRange)
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
    /// 按玩家移动预判落点，生成并投出一枚投掷物。
    /// </summary>
    protected void LaunchThrowable(Vector2 targetPosition)
    {
        Throwable throwable = entityManager.SpawnThrowable(throwableIndex, throwableSpawnPosition.position, throwBlueprint.atk * damageMultiplier, 0, -909, throwBlueprint.targetLayer);
        targetPosition += playerCharacter.Velocity * throwable.ThrowTime;
        throwable.Throw(targetPosition);
    }

    /// <summary>
    /// 死亡时先朝自身位置补投一发爆炸物，再走常规死亡流程。
    /// </summary>
    public override IEnumerator Killed(bool killedByPlayer = true)
    {
        LaunchThrowable(transform.position);
        yield return base.Killed(killedByPlayer);
    }
}
