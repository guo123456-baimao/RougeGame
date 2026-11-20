using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
[RequireComponent(typeof(Rigidbody2D))]
public class Monster : IDamageable, ISpatialHashGridClient
{
    private static readonly Color DefaultEliteTint = new Color(1f, 0.66f, 0.22f, 1f);
    [SerializeField] protected Material defaultMaterial, whiteMaterial, dissolveMaterial;
    [SerializeField] protected ParticleSystem deathParticles;
    [SerializeField] protected GameObject shadow;
    protected BoxCollider2D monsterHitbox;
    protected CircleCollider2D monsterLegsCollider;
    protected int monsterIndex;
    protected MonsterBlueprint monsterBlueprint;
    protected SpriteAnimator monsterSpriteAnimator;
    protected SpriteRenderer monsterSpriteRenderer;
    protected ZPositioner zPositioner;
    protected float currentHealth;  // 血量
    protected EntityManager entityManager;  // 怪物池
    protected Character playerCharacter;  // 角色
    protected float maxHealth;  // 满血值（Boss 血条要用，普通怪也会顺带记下）
    protected Rigidbody2D rb;
    protected int currWalkSequenceFrame = 0;
    protected bool knockedBack = false;
    protected Coroutine hitAnimationCoroutine = null;
    protected bool alive = true;
    protected Transform centerTransform;
    // ---- 精英运行时状态：只在 Setup 写入，池回收时由 ResetRuntimeState 完整还原 ----
    protected bool isElite;
    protected EliteAffix eliteAffix = EliteAffix.None;
    protected float damageMultiplier = 1f;
    protected float attackSpeedMultiplier = 1f;
    protected float moveSpeedMultiplier = 1f;
    private int eliteChestDrop;
    private Color eliteTint = Color.white;
    private Color baseSpriteColor = Color.white;
    private Vector3 baseScale = Vector3.one;
    private float affixTimer;
    private float regenPulse;
    private float shield;
    private float maxShield;
    private EliteHealthBar eliteHealthBar;
    private AfterimageTrail afterimageTrail;

    public Transform CenterTransform { get => centerTransform; }
    public UnityEvent<Monster> OnKilled { get; } = new UnityEvent<Monster>();
    public float HP => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsElite => isElite;
    public bool Alive => alive;
    public EliteAffix Affix => eliteAffix;
    public Color EliteTint => eliteTint;
    /// <summary>护盾当前值（白色血条）。</summary>
    public float Shield => shield;
    public float MaxShield => maxShield;
    public float HealthRatio => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
    public float ShieldRatio => maxShield > 0f ? Mathf.Clamp01(shield / maxShield) : 0f;
    /// <summary>精灵顶部世界坐标，精英血条要贴着头顶放。</summary>
    public float SpriteTopWorldY => monsterSpriteRenderer.bounds.max.y;
    /// <summary>Boss 会覆盖成 true，用于死亡震动等分支。</summary>
    protected virtual bool IsBoss => false;
    /// <summary>死亡时的屏幕震动幅度（世界单位），精英轻微、Boss 剧烈。</summary>
    protected virtual float DeathShakeStrength => 0.12f;
    protected virtual float DeathShakeDuration => 0.16f;
    /// <summary>本体渲染器（不是影子），血条需要跟它的排序层对齐。</summary>
    public SpriteRenderer BodyRenderer => monsterSpriteRenderer;
    /// <summary>伤害倍率，子类计算攻击时直接乘。</summary>
    public float DamageMultiplier => damageMultiplier;
    /// <summary>攻速倍率，子类计算攻击间隔时直接乘。</summary>
    public float AttackSpeedMultiplier => attackSpeedMultiplier;
    public string DisplayName => monsterBlueprint != null ? monsterBlueprint.name : "BOSS";
    // 空间哈希网格客户端接口实现
    public Vector2 Position => transform.position;
    public Vector2 Size => monsterLegsCollider.bounds.size;
    public Dictionary<int, int> ListIndexByCellIndex { get; set; }
    public int QueryID { get; set; } = -1;

    /// <summary>子类返回“已含精英倍率”的单次攻击伤害，供死亡爆炸等词缀复用。</summary>
    protected virtual float CurrentAttackDamage => 0f;

    /// <summary>
    /// 缓存刚体、碰撞体、渲染器与动画器，并记住预制体原始的缩放与颜色，便于精英化后无损还原。
    /// </summary>
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        monsterLegsCollider = GetComponent<CircleCollider2D>();
        monsterSpriteAnimator = GetComponentInChildren<SpriteAnimator>();
        monsterSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        zPositioner = gameObject.AddComponent<ZPositioner>();
        monsterHitbox = monsterSpriteRenderer.gameObject.AddComponent<BoxCollider2D>();
        monsterHitbox.isTrigger = true;
        // 记住预制体原始外观，精英化的缩放/染色才能无损还原
        baseScale = transform.localScale;
        baseSpriteColor = monsterSpriteRenderer.color;
    }

    /// <summary>
    /// 注入实体管理器与玩家，并初始化 Z 轴排序。
    /// </summary>
    public virtual void Init(EntityManager entityManager, Character playerCharacter)
    {
        this.entityManager = entityManager;
        this.playerCharacter = playerCharacter;
        zPositioner.Init(playerCharacter.transform);
    }

    /// <summary>
    /// 出场初始化：清理残留状态、应用精英配置、设置血量与碰撞体，并启动动画与移动阻力。
    /// </summary>
    public virtual void Setup(int monsterIndex, Vector2 position, MonsterBlueprint monsterBlueprint, float hpBuff = 0, EliteSpawnInfo eliteInfo = default(EliteSpawnInfo))
    {
        this.monsterIndex = monsterIndex;
        this.monsterBlueprint = monsterBlueprint;
        // 1. 先把上一位使用者留下的状态清干净，避免精英污染普通怪
        ResetRuntimeState();
        // 2. 体型必须在计算碰撞体之前生效（下面 monsterHitbox.size 用的是 spriteRenderer.bounds）
        ApplyEliteLoadout(eliteInfo);
        rb.position = position;
        transform.position = position;
        // 血量重置为满值
        maxHealth = monsterBlueprint.hp + hpBuff;
        // 3. HP 只在这里乘一次，此时 maxHealth 已经是“当前 t 下的最终 HP”
        if (isElite && eliteInfo.hpMultiplier > 0f)
            maxHealth *= eliteInfo.hpMultiplier;
        currentHealth = maxHealth;
        // 护盾词缀：护盾值 = 满血值，所以白条长度与红条完全一致
        if (isElite && eliteAffix == EliteAffix.Shield)
        {
            maxShield = maxHealth;
            shield = maxShield;
        }
        // 打开存活标记
        alive = true;
        // 加入存活怪物列表
        entityManager.LivingMonsters.Add(this);
        // 初始化动画器
        monsterSpriteAnimator.Init(monsterBlueprint.walkSpriteSequence, monsterBlueprint.walkFrameTime, true);
        // 启动并重置动画
        monsterSpriteAnimator.StartAnimating(true);
        // 确保碰撞体已启用且尺寸正确
        monsterHitbox.enabled = true;
        monsterHitbox.size = monsterSpriteRenderer.bounds.size;
        monsterHitbox.offset = Vector2.up * monsterHitbox.size.y/2;
        monsterLegsCollider.radius = monsterHitbox.size.x/2.5f;
        centerTransform = (new GameObject("Center Transform")).transform;
        centerTransform.SetParent(transform);
        centerTransform.position = transform.position + (Vector3)monsterHitbox.offset;
        // 根据加速度与移动速度设置阻力
        float spd = Random.Range(monsterBlueprint.movespeed-0.1f, monsterBlueprint.movespeed+0.1f);
        float drag = monsterBlueprint.acceleration / (spd * spd);
        // 线性阻力下终端速度 = acceleration / drag，所以 drag 除以倍率即得到线性提速
        if (moveSpeedMultiplier > 0f && !Mathf.Approximately(moveSpeedMultiplier, 1f))
            drag /= moveSpeedMultiplier;
        rb.drag = drag;
        // 重置速度
        rb.velocity = Vector2.zero;
        StopAllCoroutines();
        // 精英血条 / 残影这类纯表现件，放在碰撞体与体型都定好之后
        RefreshEliteVisuals();
    }

    /// <summary>
    /// 按玩家位置翻转朝向；精英额外驱动词缀逻辑与残影。
    /// </summary>
    protected virtual void Update()
    {
        // 朝向
        monsterSpriteRenderer.flipX = ((playerCharacter.transform.position.x - rb.position.x) < 0);
        if (!isElite)
            return;
        float deltaTime = Time.deltaTime;
        TickEliteAffix(deltaTime);
        if (eliteAffix == EliteAffix.Haste && afterimageTrail != null)
            afterimageTrail.Tick(monsterSpriteRenderer, rb.velocity, deltaTime);
    }

    /// <summary>
    /// 物理帧更新的预留钩子，由子类实现具体移动逻辑。
    /// </summary>
    protected virtual void FixedUpdate()
    {

    }

    /// <summary>
    /// /////////////////////////////////////////////////////////////////////////////
    /// 精英
    /// /////////////////////////////////////////////////////////////////////////////
    /// </summary>
    private void ApplyEliteLoadout(EliteSpawnInfo info)
    {
        // 染色与精英属性解耦：小怪只继承颜色时 isElite 仍为 false
        if (info.isElite)
            eliteTint = info.tint.a > 0f ? info.tint : DefaultEliteTint;
        else if (info.tint.a > 0f)
            eliteTint = info.tint;
        if (eliteTint.a > 0f)
            monsterSpriteRenderer.color = eliteTint;

        if (!info.isElite)
            return;

        isElite = true;
        eliteAffix = info.affix;
        damageMultiplier = info.damageMultiplier > 0f ? info.damageMultiplier : 1f;
        attackSpeedMultiplier = info.attackSpeedMultiplier > 0f ? info.attackSpeedMultiplier : 1f;
        moveSpeedMultiplier = info.moveSpeedMultiplier > 0f ? info.moveSpeedMultiplier : 1f;
        if (eliteAffix == EliteAffix.Haste)
            moveSpeedMultiplier *= EliteAffixTuning.HasteSpeedBonus;
        eliteChestDrop = info.chestDrop > 0 ? info.chestDrop : 0;
        affixTimer = 0f;
        regenPulse = 0f;

        float scale = info.scale > 0f ? info.scale : 1f;
        transform.localScale = baseScale * scale;
    }

    /// <summary>把精英/受击/死亡残留状态还原成预制体原样，池复用时会调用两次 Setup 与回收，保证幂等。</summary>
    private void ResetRuntimeState()
    {
        isElite = false;
        eliteAffix = EliteAffix.None;
        damageMultiplier = 1f;
        attackSpeedMultiplier = 1f;
        moveSpeedMultiplier = 1f;
        eliteChestDrop = 0;
        eliteTint = Color.white;
        affixTimer = 0f;
        regenPulse = 0f;
        shield = 0f;
        maxShield = 0f;
        knockedBack = false;
        hitAnimationCoroutine = null;
        maxHealth = 0f;
        currentHealth = 0f;
        transform.localScale = baseScale;
        // 出土动画会把刚体切成 Kinematic，复用时必须切回来
        if (rb != null)
            rb.bodyType = RigidbodyType2D.Dynamic;
        if (monsterSpriteRenderer != null)
        {
            monsterSpriteRenderer.color = baseSpriteColor;
            if (defaultMaterial != null)
                monsterSpriteRenderer.sharedMaterial = defaultMaterial;
            monsterSpriteRenderer.enabled = true;
        }
        if (shadow != null)
            shadow.SetActive(true);
    }

    /// <summary>回池时调用：对象池复用必须完整还原，否则会出现“精英污染普通怪”。</summary>
    public virtual void OnDespawned()
    {
        alive = false;
        if (rb != null)
            rb.velocity = Vector2.zero;
        if (afterimageTrail != null)
            afterimageTrail.Clear();
        if (eliteHealthBar != null)
            eliteHealthBar.gameObject.SetActive(false);
        ResetRuntimeState();
    }

    /// <summary>精英血条 / 黑色残影的挂载与关闭，只在 Setup 时做一次。</summary>
    private void RefreshEliteVisuals()
    {
        if (!isElite)
        {
            if (eliteHealthBar != null)
                eliteHealthBar.gameObject.SetActive(false);
            if (afterimageTrail != null)
                afterimageTrail.Clear();
            return;
        }

        if (eliteHealthBar == null)
            eliteHealthBar = EliteHealthBar.Create(this);
        eliteHealthBar.gameObject.SetActive(true);
        eliteHealthBar.RefreshLayout();

        if (eliteAffix == EliteAffix.Haste && afterimageTrail == null)
            afterimageTrail = new AfterimageTrail(entityManager != null ? entityManager.transform : null);
    }

    /// <summary>
    /// 按词缀逐帧推进：再生回血、护盾回复与周期召唤。
    /// </summary>
    private void TickEliteAffix(float deltaTime)
    {
        switch (eliteAffix)
        {
            case EliteAffix.Regeneration:
                affixTimer += deltaTime;
                if (affixTimer >= EliteAffixTuning.RegenInterval)
                {
                    affixTimer -= EliteAffixTuning.RegenInterval;
                    Heal(maxHealth * EliteAffixTuning.RegenFraction);
                }
                UpdateRegenerationTint(deltaTime);
                break;

            case EliteAffix.Shield:
                // 护盾还在才回盾；回的是白条，不动红条
                if (shield <= 0f)
                    break;
                affixTimer += deltaTime;
                if (affixTimer >= EliteAffixTuning.ShieldRegenInterval)
                {
                    affixTimer -= EliteAffixTuning.ShieldRegenInterval;
                    shield = Mathf.Min(maxShield, shield + maxShield * EliteAffixTuning.ShieldRegenFraction);
                }
                break;

            case EliteAffix.Summon:
                affixTimer += deltaTime;
                if (affixTimer >= EliteAffixTuning.SummonInterval)
                {
                    affixTimer -= EliteAffixTuning.SummonInterval;
                    SummonMinion();
                }
                break;
        }
    }

    /// <summary>再生回血时颜色在精英色与浅绿之间往返，回满立刻归位。</summary>
    private void UpdateRegenerationTint(float deltaTime)
    {
        if (currentHealth >= maxHealth)
        {
            if (regenPulse > 0f)
            {
                regenPulse = 0f;
                monsterSpriteRenderer.color = eliteTint;
            }
            return;
        }
        regenPulse += deltaTime * EliteAffixTuning.RegenPulseSpeed;
        monsterSpriteRenderer.color = Color.Lerp(eliteTint, EliteAffixTuning.RegenTint, Mathf.PingPong(regenPulse, 1f));
    }

    /// <summary>
    /// 在存活且未满血时恢复生命，不会超过上限。
    /// </summary>
    private void Heal(float amount)
    {
        if (!alive || currentHealth >= maxHealth || amount <= 0f)
            return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    /// <summary>
    /// 在自身周围随机位置召唤一只继承染色的小怪。
    /// </summary>
    private void SummonMinion()
    {
        if (!alive || entityManager == null || monsterBlueprint == null)
            return;
        Vector2 spawnPosition = (Vector2)transform.position + Random.insideUnitCircle * EliteAffixTuning.SummonSpawnRadius;
        SpawnOffspring(spawnPosition, 0f);
    }

    /// <summary>
    /// 分裂 / 召唤出的小怪：继承精英染色、从地面缓缓升起，
    /// 但 isElite 为 false —— 不继承倍率、不再带词缀、也不算进精英计数。
    /// </summary>
    private void SpawnOffspring(Vector2 spawnPosition, float hpBuff)
    {
        if (entityManager == null || monsterBlueprint == null)
            return;
        EliteSpawnInfo cosmetic = default(EliteSpawnInfo);
        cosmetic.tint = eliteTint;
        Monster child = entityManager.SpawnMonster(monsterIndex, spawnPosition, monsterBlueprint, hpBuff, cosmetic);
        if (child != null)
            child.PlayEmergeAnimation();
    }

    /// <summary>从地面下方缓缓升起并淡入，用于分裂 / 召唤出的小怪。</summary>
    public void PlayEmergeAnimation()
    {
        if (!gameObject.activeInHierarchy)
            return;
        StartCoroutine(EmergeRoutine());
    }

    /// <summary>
    /// 从地面下方升起并淡入，结束后恢复正常刚体类型与受击盒。
    /// </summary>
    private IEnumerator EmergeRoutine()
    {
        Vector2 target = rb.position;
        Vector2 start = target - Vector2.up * EliteAffixTuning.EmergeHeight;
        Color tint = monsterSpriteRenderer.color;
        float duration = Mathf.Max(0.01f, EliteAffixTuning.EmergeDuration);

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.velocity = Vector2.zero;
        monsterHitbox.enabled = false;
        rb.position = start;

        float t = 0f;
        while (t < duration && alive)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float eased = 1f - (1f - k) * (1f - k);
            rb.position = Vector2.Lerp(start, target, eased);
            monsterSpriteRenderer.color = new Color(tint.r, tint.g, tint.b, k);
            yield return null;
        }

        if (!alive)
            yield break;

        rb.position = target;
        monsterSpriteRenderer.color = tint;
        rb.bodyType = RigidbodyType2D.Dynamic;
        monsterHitbox.enabled = true;
    }

    /// <summary>
    /// 按词缀触发死亡效果：分裂或死亡爆炸。
    /// </summary>
    private void TriggerDeathAffix()
    {
        switch (eliteAffix)
        {
            case EliteAffix.Split:
                SpawnSplitChildren();
                break;

            case EliteAffix.DeathExplosion:
                ExplodeOnDeath();
                break;
        }
    }

    /// <summary>
    /// 在尸体周围生成若干只继承 30% 血量的小怪。
    /// </summary>
    private void SpawnSplitChildren()
    {
        if (entityManager == null || monsterBlueprint == null)
            return;
        // maxHealth 此时已是精英的最终血量，孩子取它的 30%
        float childMaxHealth = maxHealth * EliteAffixTuning.SplitChildHealthFraction;
        float childHpBuff = childMaxHealth - monsterBlueprint.hp;
        Vector2 origin = transform.position;
        for (int i = 0; i < EliteAffixTuning.SplitChildCount; i++)
        {
            Vector2 spawnPosition = origin + Random.insideUnitCircle * EliteAffixTuning.SplitChildSpawnRadius;
            SpawnOffspring(spawnPosition, childHpBuff);
        }
    }

    /// <summary>死亡爆炸：交给独立的表现对象，先画范围预警，延迟结束再结算伤害与击退。</summary>
    private void ExplodeOnDeath()
    {
        if (playerCharacter == null)
            return;
        float damage = CurrentAttackDamage * EliteAffixTuning.DeathExplosionDamageScale;
        DeathExplosionEffect.Spawn(playerCharacter, transform.position,
            EliteAffixTuning.DeathExplosionRadius, damage,
            EliteAffixTuning.DeathExplosionKnockback, EliteAffixTuning.DeathExplosionDelay);
    }

    /// <summary>
    /// 按概率掉落精英宝箱，数量由精英配置决定。
    /// </summary>
    private void DropEliteChest()
    {
        if (eliteChestDrop <= 0 || entityManager == null)
            return;
        // 掉落概率单独掷骰：没掷中就一只都不掉
        if (Random.value >= EliteAffixTuning.EliteChestDropChance)
            return;
        ChestBlueprint chestBlueprint = entityManager.DefaultChestBlueprint;
        if (chestBlueprint == null)
            return;
        Vector2 origin = transform.position;
        for (int i = 0; i < eliteChestDrop; i++)
        {
            Vector2 spawnPosition = origin + Random.insideUnitCircle * 0.4f;
            entityManager.SpawnChest(chestBlueprint, spawnPosition);
        }
    }

    /// <summary>
    /// 按当前阻力换算并施加击退速度。
    /// </summary>
    public override void Knockback(Vector2 knockback)
    {
        rb.velocity += knockback * Mathf.Sqrt(rb.drag);
    }

    /// <summary>
    /// 结算伤害：护盾先吸收、显示飘字并播放受击反馈，血量归零则进入死亡流程。
    /// </summary>
    public override void TakeDamage(float damage, Vector2 knockback = default(Vector2))
    {
        if (alive)
        {
            // 护盾：白条先吃伤害，不做任何额外减伤
            float absorbed = 0f;
            if (isElite && shield > 0f)
            {
                absorbed = Mathf.Min(shield, damage);
                shield -= absorbed;
                damage -= absorbed;
            }
            entityManager.SpawnDamageText(monsterHitbox.transform.position, absorbed > 0f ? absorbed : damage);
            if (damage > 0f)
                currentHealth -= damage;
            if (hitAnimationCoroutine != null) StopCoroutine(hitAnimationCoroutine);
            if (knockback != default(Vector2))
            {
                rb.velocity += knockback * Mathf.Sqrt(rb.drag);
                knockedBack = true;
            }
            if (currentHealth > 0)
                hitAnimationCoroutine = StartCoroutine(HitAnimation());
            else
                StartCoroutine(Killed());
        }
    }

    /// <summary>
    /// 受击闪白 0.15 秒后还原材质，并清除击退标记。
    /// </summary>
    protected IEnumerator HitAnimation()
    {
        monsterSpriteRenderer.sharedMaterial = whiteMaterial;
        yield return new WaitForSeconds(0.15f);
        monsterSpriteRenderer.sharedMaterial = defaultMaterial;
        knockedBack = false;
    }

    /// <summary>
    /// 死亡流程：震屏、关闭受击盒、掉落战利品、播放死亡动画后回池。
    /// </summary>
    public virtual IEnumerator Killed(bool killedByPlayer = true)
    {
        // 精英 / Boss 死亡时抖一下屏幕，普通怪不抖
        if (isElite || IsBoss)
            CameraShake.Shake(DeathShakeStrength, DeathShakeDuration);
        // 关闭存活标记并禁用受击盒
        alive = false;
        monsterHitbox.enabled = false;
        if (eliteHealthBar != null)
            eliteHealthBar.gameObject.SetActive(false);
        // 从存活怪物列表中移除
        entityManager.LivingMonsters.Remove(this);
        // 掉落战利品
        if (killedByPlayer)
        {
            DropLoot();
            if (isElite)
            {
                DropEliteChest();
                TriggerDeathAffix();
            }
        }

        if (deathParticles != null)
        {       
            deathParticles.Play();
        }

        yield return HitAnimation();

        if (deathParticles != null)
        {
            monsterSpriteRenderer.enabled = false;
            shadow.SetActive(false);
            yield return new WaitForSeconds(deathParticles.main.duration - 0.15f);
            monsterSpriteRenderer.enabled = true;
            shadow.SetActive(true);
        }
        // monsterSpriteRenderer.material = dissolveMaterial;
        // float t = 0;
        // while (t < 1)
        // {
        //     monsterSpriteRenderer.material.SetFloat("_Dissolve", t);
        //     t += Time.deltaTime*2;
        //     yield return null;
        // }
        // monsterSpriteRenderer.sharedMaterial = defaultMaterial;
        //yield return new WaitForSeconds(0.2f);

        // 触发怪物被击杀回调，并移除所有监听
        OnKilled.Invoke(this);
        OnKilled.RemoveAllListeners();
        entityManager.DespawnMonster(monsterIndex, this, true);
    }

    /// <summary>
    /// 按掉落表生成经验宝石与金币。
    /// </summary>
    protected virtual void DropLoot()
    {
        if (monsterBlueprint.gemLootTable.TryDropLoot(out GemType gemType))
            entityManager.SpawnExpGem((Vector2)transform.position, gemType);
        if (monsterBlueprint.coinLootTable.TryDropLoot(out CoinType coinType))
            entityManager.SpawnCoin((Vector2)transform.position, coinType);
    }
}
