using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;
public class Character : IDamageable, ISpatialHashGridClient
{
    [Header("Dependencies")]
    [SerializeField] protected Transform centerTransform;
    [SerializeField] protected Transform lookIndicator;
    [SerializeField] protected float lookIndicatorRadius;
    [SerializeField] protected TextMeshProUGUI levelText;
    [SerializeField] protected AbilitySelectionDialog abilitySelectionDialog;
    [SerializeField] protected PointBar healthBar;
    [SerializeField] protected PointBar expBar;
    [SerializeField] protected Collider2D collectableCollider;
    [SerializeField] protected Collider2D meleeHitboxCollider;
    [SerializeField] protected ParticleSystem dustParticles;
    [SerializeField] protected Material defaultMaterial, hitMaterial, deathMaterial;
    [SerializeField] protected ParticleSystem deathParticles;
    protected CharacterBlueprint characterBlueprint;
    protected UpgradeableMovementSpeed movementSpeed;
    protected UpgradeableArmor armor;
    protected bool alive = true;
    protected int currentLevel = 1;
    protected float currentExp = 0;
    protected float nextLevelExp = 5;
    protected float expToNextLevel = 5;
    protected float currentHealth;
    protected float baseHpForEngravings;
    protected SpriteRenderer spriteRenderer;
    protected SpriteAnimator spriteAnimator;
    protected AbilityManager abilityManager;
    protected EntityManager entityManager;
    protected StatsManager statsManager;
    protected Rigidbody2D rb;
    protected ZPositioner zPositioner;
    protected Vector2 lookDirection = Vector2.right;
    protected CoroutineQueue coroutineQueue;
    protected Coroutine hitAnimationCoroutine = null;
    protected Vector2 moveDirection;
    protected float engravingLifestealChance;
    protected float engravingLifestealAmount;
    /// <summary>
    /// 护甲最多吃掉的比例：无论护甲多高，至少有 25% 的伤害穿透。
    /// 差值式减伤在「护甲 ≥ 攻击」时会触底到 1 点，叠 10 点护甲就等于对小怪无敌。
    /// </summary>
    private const float ArmorDamageFloor = 0.25f;
    public Vector2 LookDirection
    {
        get { return lookDirection; }
        set
        {
            if (value != Vector2.zero)
                lookDirection = value;
        }
    }
    public Transform CenterTransform { get => centerTransform; }
    public Collider2D CollectableCollider { get => collectableCollider; }
    public float Luck { get; private set; }
    public int CurrentLevel { get => currentLevel; }
    /// <summary>当前血量占上限的比例，HUD 用它决定要不要脉冲告警。</summary>
    public float HealthRatio => baseHpForEngravings > 0f ? Mathf.Clamp01(currentHealth / baseHpForEngravings) : 0f;
    /// <summary>铭文与商店加成结算后的实际最大生命，环境伤害按它计算。</summary>
    public float MaxHealth => baseHpForEngravings;
    public UnityEvent<float> OnDealDamage { get; } = new UnityEvent<float>();
    public UnityEvent OnDeath { get; } = new UnityEvent();
    public CharacterBlueprint Blueprint { get => characterBlueprint; }
    public Vector2 Velocity { get => rb.velocity; }
    public Vector2 Position => transform.position;
    public Vector2 Size => meleeHitboxCollider.bounds.size;
    public Dictionary<int, int> ListIndexByCellIndex { get; set; }
    public int QueryID { get; set; } = -1;

    /// <summary>
    /// 缓存刚体、Z 轴排序与动画器，并从跨场景数据读取角色蓝图与幸运值。
    /// </summary>
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        zPositioner = gameObject.AddComponent<ZPositioner>();
        spriteAnimator = GetComponentInChildren<SpriteAnimator>();
        spriteRenderer = spriteAnimator.GetComponent<SpriteRenderer>();
        characterBlueprint = CrossSceneData.CharacterBlueprint;
        Luck = characterBlueprint.luck;
    }

    /// <summary>
    /// 注入各管理器并初始化血条、经验条、移速与护甲，随后依次应用铭文、Z 轴排序、商店加成与吸血。
    /// </summary>
    public virtual void Init(EntityManager entityManager, AbilityManager abilityManager, StatsManager statsManager)
    {
        this.entityManager = entityManager;
        this.abilityManager = abilityManager;
        this.statsManager = statsManager;
        OnDealDamage.AddListener(statsManager.IncreaseDamageDealt);
        coroutineQueue = new CoroutineQueue(this);
        coroutineQueue.StartLoop();

        baseHpForEngravings = characterBlueprint.hp;

        currentHealth = baseHpForEngravings;
        healthBar.Setup(currentHealth, 0, baseHpForEngravings);
        // 经验条见底是常态，只有血量条需要在见底时告警
        healthBar.SetPulseWhenLow(true);
        expBar.Setup(currentExp, 0, nextLevelExp);
        currentLevel = 1;
        UpdateLevelDisplay();
        spriteAnimator.Init(characterBlueprint.walkSpriteSequence, characterBlueprint.walkFrameTime, false);
        movementSpeed = new UpgradeableMovementSpeed();
        movementSpeed.Value = characterBlueprint.movespeed;
        abilityManager.RegisterUpgradeableValue(movementSpeed, true);
        UpdateMoveSpeed();
        armor = new UpgradeableArmor();
        armor.Value = characterBlueprint.armor;
        abilityManager.RegisterUpgradeableValue(armor, true);

        // 铭文结算要写 movementSpeed / armor / healthBar，必须等这三者初始化完之后再调用，
        // 否则会空引用并中断后面 zPositioner / 商店加成 / 吸血 的初始化。
        ApplyEngravings();

        zPositioner.Init(transform);
        ApplySessionBoosts();
        ApplyEngravingLifesteal();
    }

    /// <summary>
    /// 汇总已装备铭文的增益与诅咒，算出最终生命、伤害、速度、护甲与幸运后一次性应用。
    /// </summary>
    private void ApplyEngravings()
    {
        var selected = EngravingManager.SelectedEngravings;
        if (selected.Count == 0)
        {
            // 没装铭文时把上一局可能残留的倍率复位
            EngravingManager.DamageMultiplier = 1f;
            return;
        }

        // 铭文统一从全局铭文库取
        var pool = EngravingManager.AllEngravings;

        float totalHpMult = 1f;
        float totalDmgMult = 1f;
        float totalSpeedMult = 1f;
        float totalArmor = 0f;
        float totalLuck = 0f;
        engravingLifestealChance = 0f;
        engravingLifestealAmount = 0f;
        float curseHpMult = 1f;
        float curseDmgMult = 1f;
        float curseSpeedMult = 1f;
        float curseArmor = 0f;

        foreach (var id in selected)
        {
            var eng = EngravingManager.GetEngraving(id, pool);
            if (eng == null) continue;
            totalHpMult *= eng.hpMultiplier;
            totalDmgMult *= eng.damageMultiplier;
            totalSpeedMult *= eng.speedMultiplier;
            totalArmor += eng.armorValue;
            totalLuck += eng.luckValue;
            engravingLifestealChance += eng.lifestealChance;
            engravingLifestealAmount += eng.lifestealAmount;
            curseHpMult *= eng.curseHpMultiplier;
            curseDmgMult *= eng.curseDamageMultiplier;
            curseSpeedMult *= eng.curseSpeedMultiplier;
            curseArmor += eng.curseArmorValue;
        }

        float finalHp = baseHpForEngravings * totalHpMult * curseHpMult;
        float finalDmgMult = totalDmgMult * curseDmgMult;
        float finalSpeedMult = totalSpeedMult * curseSpeedMult;
        float finalArmor = totalArmor + curseArmor;
        float finalLuck = characterBlueprint.luck + totalLuck;

        Debug.Log("[Engraving] " + characterBlueprint.name + " 装备 " + string.Join(", ", selected) +
                  " → HP x" + (finalHp / baseHpForEngravings).ToString("0.##") +
                  " 伤害 x" + finalDmgMult.ToString("0.##") +
                  " 速度 x" + finalSpeedMult.ToString("0.##") +
                  " 护甲 " + finalArmor.ToString("0.##") +
                  " 幸运 " + finalLuck.ToString("0.##"));

        ApplyEngravingStats(finalHp, finalDmgMult, finalSpeedMult,
            engravingLifestealChance, engravingLifestealAmount, finalArmor, finalLuck);
    }

    /// <summary>
    /// 把铭文结算结果写入角色：生命上限、伤害倍率、移速、护甲与幸运。
    /// </summary>
    public void ApplyEngravingStats(float finalHp, float dmgMult, float speedMult,
        float lifestealChance, float lifestealAmount, float finalArmor, float finalLuck)
    {
        float hpDiff = finalHp - baseHpForEngravings;
        currentHealth += hpDiff;
        healthBar.Setup(currentHealth, 0, finalHp);
        baseHpForEngravings = finalHp;

        // 伤害倍率由 UpgradeableDamage / UpgradeableBleedDamage 读取时统一套用
        EngravingManager.DamageMultiplier = dmgMult;

        movementSpeed.Value = characterBlueprint.movespeed * speedMult;
        UpdateMoveSpeed();

        armor.Value += (int)finalArmor;

        this.engravingLifestealChance = lifestealChance;
        this.engravingLifestealAmount = lifestealAmount;

        Luck = finalLuck;
    }

    /// <summary>
    /// 铭文带吸血属性时注册「造成伤害」回调。
    /// </summary>
    private void ApplyEngravingLifesteal()
    {
        if (engravingLifestealChance > 0f && engravingLifestealAmount > 0f)
        {
            OnDealDamage.AddListener(OnPlayerDealtDamage);
        }
    }

    /// <summary>
    /// 按吸血概率把造成的伤害转化为自身治疗。
    /// </summary>
    private void OnPlayerDealtDamage(float damage)
    {
        if (!alive) return;
        if (Random.Range(0.0f, 1.0f) < engravingLifestealChance)
        {
            GainHealth(engravingLifestealAmount);
        }
    }

    /// <summary>
    /// 把本局商店购买的属性加成发到角色身上，并立即把存档里的加成清零。
    /// </summary>
    private void ApplySessionBoosts()
    {
        float hpBoost = RunState.HpBoost;
        if (hpBoost > 0)
        {
            currentHealth += hpBoost;
            healthBar.Setup(currentHealth, 0, baseHpForEngravings + hpBoost);
            RunState.HpBoost = 0f;
        }
        int armorBoost = RunState.ArmorBoost;
        if (armorBoost > 0)
        {
            armor.Value += armorBoost;
            RunState.ArmorBoost = 0;
        }
        float speedBoost = RunState.MoveSpeedBoost;
        if (speedBoost > 0)
        {
            // 商店「速度 +N」按百分比结算（HUD 也写成 +N%）。
            // 直接 += N 会把 1.15 抬到 2.15，等于白送 87% 移速，比任何道具都强。
            movementSpeed.Value *= 1f + speedBoost * 0.01f;
            UpdateMoveSpeed();
            RunState.MoveSpeedBoost = 0f;
        }
        float luckBoost = RunState.LuckBoost;
        if (luckBoost > 0)
        {
            Luck += luckBoost;
            RunState.LuckBoost = 0f;
        }

        // 加成已经发到身上了，存档里要同步归零：
        // 否则中途强杀游戏再登录，快照里还留着这些加成，能反复白嫖叠加
        ShopSession.Save();
    }

    /// <summary>
    /// 一局结束：清空单局内的属性加成、消耗品与购买记录。
    /// </summary>
    public static void ClearSessionData()
    {
        // 一局结束：清空单局内存状态（属性加成 / 消耗品 / 单局购买记录）
        RunState.Clear();
        ShopSession.Clear();
    }

    /// <summary>
    /// 刷新瞄准指示器位置与角色翻转（异步初始化完成前跳过）。
    /// </summary>
    protected virtual void Update()
    {
        if (statsManager == null) return;  // 异步初始化完成前不刷朝向表现
        lookIndicator.transform.localPosition = lookDirection * lookIndicatorRadius;
        spriteRenderer.flipX = lookDirection.x < 0;
    }

    /// <summary>
    /// 按输入方向加速角色，并维护朝向与行走动画。
    /// </summary>
    protected virtual void FixedUpdate()
    {
        if (statsManager == null) return;  // 异步初始化（Init）完成前不参与物理移动
        if (moveDirection != Vector2.zero)
            lookDirection = moveDirection;
        else
            StopWalkAnimation();
        if (alive)
            rb.velocity += moveDirection * characterBlueprint.acceleration * Time.deltaTime;
    }

    /// <summary>
    /// 获得经验，并把可能连续触发的升级排队处理。
    /// </summary>
    public void GainExp(float exp)
    {
        if (alive)
            coroutineQueue.EnqueueCoroutine(GainExpCoroutine(exp));
    }

    /// <summary>
    /// 逐级消耗经验并触发升级，升级后重算下一级所需经验。
    /// </summary>
    private IEnumerator GainExpCoroutine(float exp)
    {
        if (alive)
        {
            while (currentExp + exp >= nextLevelExp)
            {
                float expDiff = nextLevelExp - currentExp;
                currentExp += expDiff;
                exp -= expDiff;
                expBar.Setup(currentExp, 0, nextLevelExp);
                yield return LevelUpCoroutine();
                float prevLevelExp = nextLevelExp;
                expToNextLevel += characterBlueprint.LevelToExpIncrease(currentLevel);
                nextLevelExp += expToNextLevel;
                expBar.Setup(currentExp, prevLevelExp, nextLevelExp);
            }
            currentExp += exp;
            expBar.AddPoints(exp);
        }
    }

    /// <summary>
    /// 升级流程：等级 +1、刷新 HUD 与音效，并打开技能选择界面直到玩家选完。
    /// </summary>
    private IEnumerator LevelUpCoroutine()
    {
        if (alive)
        {
            currentLevel++;
            UpdateLevelDisplay();
            InGameHud.Instance?.FlashLevelUp();
            AudioManager.Instance.PlayLevelUp();
            abilitySelectionDialog.Open();
            while (abilitySelectionDialog.MenuOpen)
            {
                yield return null;
            }
        }
    }

    /// <summary>
    /// 刷新等级文本。
    /// </summary>
    private void UpdateLevelDisplay()
    {
        levelText.text = "LV " + currentLevel;
    }

    /// <summary>
    /// 按阻力换算并施加击退速度。
    /// </summary>
    public override void Knockback(Vector2 knockback)
    {
        rb.velocity += knockback * Mathf.Sqrt(rb.drag);
    }

    /// <summary>
    /// 结算伤害：护甲按比例减免（至少 25% 穿透）、刷新血条与受击表现，血量归零则播放死亡动画。
    /// </summary>
    public override void TakeDamage(float damage, Vector2 knockback = default(Vector2))
    {
        if (alive)
        {
            damage = Mathf.Max(damage - armor.Value, damage * ArmorDamageFloor);
            healthBar.SubtractPoints(damage);
            currentHealth -= damage;
            rb.velocity += knockback * Mathf.Sqrt(rb.drag);
            statsManager.IncreaseDamageTaken(damage);
            InGameHud.Instance?.FlashDamage();
            if (currentHealth <= 0)
            {
                StartCoroutine(DeathAnimation());
            }
            else
            {
                if (hitAnimationCoroutine != null) StopCoroutine(hitAnimationCoroutine);
                hitAnimationCoroutine = StartCoroutine(HitAnimation());
            }
        }
    }

    /// <summary>
    /// 环境类真实伤害：不再经过护甲减免，用于血雨这类按最大生命百分比结算的机制。
    /// </summary>
    public void TakeTrueDamage(float damage)
    {
        if (!alive || damage <= 0f) return;
        damage = Mathf.Min(damage, Mathf.Max(0f, currentHealth));
        healthBar.SubtractPoints(damage);
        currentHealth -= damage;
        statsManager.IncreaseDamageTaken(damage);
        InGameHud.Instance?.FlashDamage();
        if (currentHealth <= 0f)
        {
            StartCoroutine(DeathAnimation());
        }
        else
        {
            if (hitAnimationCoroutine != null) StopCoroutine(hitAnimationCoroutine);
            hitAnimationCoroutine = StartCoroutine(HitAnimation());
        }
    }

    /// <summary>
    /// 受击闪红 0.15 秒后还原材质。
    /// </summary>
    private IEnumerator HitAnimation()
    {
        spriteRenderer.sharedMaterial = hitMaterial;
        yield return new WaitForSeconds(0.15f);
        spriteRenderer.sharedMaterial = defaultMaterial;
    }

    /// <summary>
    /// 死亡流程：销毁技能、播放擦除特效与音效，清理单局数据并触发死亡事件。
    /// </summary>
    private IEnumerator DeathAnimation()
    {
        alive = false;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayDefeat();
        spriteRenderer.sharedMaterial = deathMaterial;

        abilityManager.DestroyActiveAbilities();
        StopWalkAnimation();
        deathParticles.Play();
        float height = spriteRenderer.bounds.size.y;
        float t = 0;
        while (t < 1)
        {
            spriteRenderer.sharedMaterial = deathMaterial;
            deathParticles.transform.position = transform.position + Vector3.up * height * (1-t);
            deathMaterial.SetFloat("_Wipe", t);
            t += Time.deltaTime;
            yield return null;
        }
        deathMaterial.SetFloat("_Wipe", 1.0f);

        yield return new WaitForSeconds(0.5f);

        ClearSessionData();
        OnDeath.Invoke();
        spriteRenderer.enabled = false;
    }

    /// <summary>
    /// 回复生命并刷新血条，不会超过上限。
    /// </summary>
    public void GainHealth(float health)
    {
        healthBar.AddPoints(health);
        currentHealth += health;
        if (currentHealth > baseHpForEngravings)
            currentHealth = baseHpForEngravings;
    }

    /// <summary>
    /// 读取瞄准输入并更新朝向。
    /// </summary>
    public void SetLookDirecton(InputAction.CallbackContext context)
    {
        LookDirection = context.ReadValue<Vector2>();
    }

    /// <summary>
    /// 用加速度与移速反推刚体阻力，使实际最高速度匹配当前移速。
    /// </summary>
    public void UpdateMoveSpeed()
    {
        rb.drag = characterBlueprint.acceleration / (movementSpeed.Value * movementSpeed.Value);
    }

    /// <summary>
    /// 缓存输入给出的移动方向。
    /// </summary>
    public void Move(Vector2 moveDirection)
    {
        this.moveDirection = moveDirection;
    }

    /// <summary>
    /// 存活时开始播放行走动画。
    /// </summary>
    public void StartWalkAnimation()
    {
        if (alive)
            spriteAnimator.StartAnimating();
    }

    /// <summary>
    /// 停止行走动画并回到待机帧。
    /// </summary>
    public void StopWalkAnimation()
    {
        spriteAnimator.StopAnimating(true);
    }

    /// <summary>
    /// 把输入系统的移动向量归一化后缓存。
    /// </summary>
    public void SetMoveDirection(InputAction.CallbackContext context)
    {
        moveDirection = context.action.ReadValue<Vector2>().normalized;
    }
}
