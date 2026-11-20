using System.Collections;
using UnityEngine;
public class GarlicAbility : Ability
{
    [Header("Garlic Stats")]
    [SerializeField] protected LayerMask monsterLayer;
    [SerializeField] protected UpgradeableDamage damage;
    [SerializeField] protected UpgradeableAOE radius;
    [SerializeField] protected UpgradeableDamageRate damageRate;
    [SerializeField] protected UpgradeableKnockback knockback;
    private float timeSinceLastAttack;
    private FastList<GameObject> hitMonsters;

    public override bool HasCooldown { get => true; }
    public override float CooldownProgress
    {
        get
        {
            float period = damageRate.Value > 0f ? 1f / damageRate.Value : 0f;
            return period > 0f ? Mathf.Clamp01(timeSinceLastAttack / period) : 1f;
        }
    }
    private CircleCollider2D damageCollider;
    private SpriteRenderer spriteRenderer;

    /// <summary>
    /// 缓存光环碰撞体与精灵渲染器引用。
    /// </summary>
    void Awake()
    {
        damageCollider = GetComponent<CircleCollider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    /// <summary>
    /// 初始化光环碰撞半径与编辑器数据。
    /// </summary>
    public override void Init(AbilityManager abilityManager, EntityManager entityManager, Character playerCharacter)
    {
        base.Init(abilityManager, entityManager, playerCharacter);
        transform.SetParent(playerCharacter.transform);
        transform.localPosition = Vector3.zero;
    }

    /// <summary>
    /// 启动持续伤害光环。
    /// </summary>
    protected override void Use()
    {
        base.Use();
        gameObject.SetActive(true);
        hitMonsters = new FastList<GameObject>();
        damageCollider.radius = radius.Value;
        spriteRenderer.transform.localScale = Vector3.one * radius.Value * 2;
    }

    /// <summary>
    /// 升级光环半径与伤害频率。
    /// </summary>
    protected override void Upgrade()
    {
        base.Upgrade();
        damageCollider.radius = radius.Value;
        spriteRenderer.transform.localScale = Vector3.one * radius.Value * 2;
    }

    /// <summary>
    /// 按伤害频率对光环范围内的敌人周期结算伤害。
    /// </summary>
    void Update()
    {
        timeSinceLastAttack += Time.deltaTime;
        if (timeSinceLastAttack >= 1/damageRate.Value)
        {
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, radius.Value, monsterLayer);
            foreach (Collider2D collider in hitColliders)
            {
                Damage(collider.GetComponentInParent<IDamageable>());
            }
            timeSinceLastAttack = Mathf.Repeat(timeSinceLastAttack, 1/damageRate.Value);
        }
    }

    /// <summary>
    /// 对光环内的敌人结算伤害，并登记其离开时的清理。
    /// </summary>
    private void Damage(IDamageable damageable)
    {
        Vector2 knockbackDirection = (damageable.transform.position - transform.position).normalized;
        damageable.TakeDamage(damage.Value, knockback.Value * knockbackDirection);
        playerCharacter.OnDealDamage.Invoke(damage.Value);
    }

    /// <summary>
    /// 怪物死亡或离开光环时移除登记。
    /// </summary>
    private void DeregisterMonster(Monster monster)
    {
        hitMonsters.Remove(monster.gameObject);
    }

    /// <summary>
    /// 敌人进入光环时登记到命中表并立即结算一次伤害。
    /// </summary>
    void OnTriggerEnter2D(Collider2D collider)
    {
        if (!hitMonsters.Contains(collider.gameObject) && (monsterLayer & (1 << collider.gameObject.layer)) != 0)
        {
            hitMonsters.Add(collider.gameObject);
            Monster monster = collider.gameObject.GetComponentInParent<Monster>();
            monster.OnKilled.AddListener(DeregisterMonster);
            Damage(monster);
        }
    }
}
