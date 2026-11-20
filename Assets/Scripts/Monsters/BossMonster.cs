using System.Collections;
using System.Linq;
using UnityEngine;
public class BossMonster : Monster
{
    protected BossAbility[] abilities;
    protected Coroutine act = null;
    public Rigidbody2D Rigidbody { get => rb; }
    public SpriteAnimator Animator { get => monsterSpriteAnimator; }
    protected float timeSinceLastMeleeAttack;

    protected BossMonsterBlueprint bossBlueprint => (BossMonsterBlueprint)monsterBlueprint;

    // Boss 死亡：剧烈震动（远超精英的轻微抖动）
    protected override bool IsBoss => true;
    protected override float DeathShakeStrength => 0.7f;
    protected override float DeathShakeDuration => 0.7f;

    /// <summary>
    /// 初始化 Boss 技能实例并启动技能选择循环。
    /// </summary>
    public override void Setup(int monsterIndex, Vector2 position, MonsterBlueprint monsterBlueprint, float hpBuff = 0, EliteSpawnInfo eliteInfo = default(EliteSpawnInfo))
    {
        // Boss 走独立的池与刷怪入口，永远不会被精英化，这里只是签名对齐
        base.Setup(monsterIndex, position, monsterBlueprint, hpBuff, eliteInfo);
        abilities = new BossAbility[bossBlueprint.abilityPrefabs.Length];
        for (int i = 0; i < abilities.Length; i++)
        {
            abilities[i] = Instantiate(bossBlueprint.abilityPrefabs[i], transform).GetComponent<BossAbility>();
            abilities[i].Init(this, entityManager, playerCharacter);
        }
        act = StartCoroutine(Act());
    }

    /// <summary>
    /// 每帧累加近战攻击冷却计时。
    /// </summary>
    protected override void Update()
    {
        base.Update();
        timeSinceLastMeleeAttack += Time.deltaTime;
    }
    // protected override void FixedUpdate()
    // {
    //     base.FixedUpdate();
    //     // Vector2 moveDirection = (playerCharacter.transform.position - transform.position).normalized;
    //     // rb.velocity += moveDirection * monsterBlueprint.acceleration * Time.fixedDeltaTime;
    // }

    /// <summary>
    /// 按加速度朝指定方向移动（由技能驱动）。
    /// </summary>
    public void Move(Vector2 direction, float deltaTime)
    {
        rb.velocity += direction * bossBlueprint.acceleration * deltaTime;
    }

    /// <summary>
    /// 立即清零速度，让 Boss 停下。
    /// </summary>
    public void Freeze()
    {
        rb.velocity = Vector2.zero;
    }

    /// <summary>
    /// 循环按技能评分加权随机挑一个技能执行，全部评分为 0 时等待一秒。
    /// </summary>
    private IEnumerator Act()
    {
        while (true)
        {
            float[] abilityScores = abilities.Select(a => a.Score()).ToArray();
            float totalScore = abilityScores.Sum();
            float rand = Random.Range(0f, totalScore);
            float cumulative = 0;
            int abilityIndex = -1;
            for (int i = 0; i < abilities.Length; i++)
            {
                abilities[i].Deactivate();
                cumulative += abilityScores[i];
                if (abilityIndex == -1 && rand < cumulative)
                    abilityIndex = i;
            }
            if (abilityIndex == -1)
            {
                Debug.Log(totalScore);
                yield return new WaitForSeconds(1);
            }
            else
                yield return abilities[abilityIndex].Activate();
        }
    }

    /// <summary>
    /// 在基类掉落之外，额外在尸体位置生成 Boss 宝箱。
    /// </summary>
    protected override void DropLoot()
    {
        base.DropLoot();
        if (bossBlueprint.chestBlueprint != null)
            entityManager.SpawnChest(bossBlueprint.chestBlueprint, transform.position);
    }

    /// <summary>
    /// 死亡时销毁所有技能实例、停止技能循环，再走常规死亡流程。
    /// </summary>
    public override IEnumerator Killed(bool killedByPlayer = true)
    {
        foreach (BossAbility ability in abilities)
            Destroy(ability.gameObject);
        StopCoroutine(act);
        yield return base.Killed(killedByPlayer);
    }

    /// <summary>
    /// 按碰撞层结算近战伤害（冷却未好时只推不伤），碰到宝箱则直接打开。
    /// </summary>
    void OnCollisionEnter2D(Collision2D col)
    {
        if (((bossBlueprint.meleeLayer & (1 << col.collider.gameObject.layer)) != 0))
        {
            IDamageable damageable = col.collider.GetComponentInParent<IDamageable>();
            Vector2 knockbackDirection = (damageable.transform.position - transform.position).normalized;
            if (timeSinceLastMeleeAttack > bossBlueprint.meleeAttackDelay)
            {
                damageable.TakeDamage(bossBlueprint.meleeDamage, bossBlueprint.meleeKnockback * knockbackDirection);
                timeSinceLastMeleeAttack = 0;
            }
            else
            {
                damageable.TakeDamage(0, bossBlueprint.meleeKnockback * knockbackDirection);
            }
        }

        if (col.gameObject.TryGetComponent<Chest>(out Chest chest))
        {
            chest.OpenChest(false);
        }
    }
}
