using System.Collections;
using UnityEngine;
public class GrenadeBossAbility : BossAbility
{
    [Header("Grenade Details")]
    [SerializeField] protected GameObject grenadePrefab;
    [SerializeField] protected LayerMask targetLayer;
    [SerializeField] protected float damage;
    [SerializeField] protected float knockback;
    [SerializeField] protected float fireRate;
    [Tooltip("一次投几颗")]
    [SerializeField] protected int grenadeCount = 3;
    [Tooltip("同一轮里每颗之间的间隔")]
    [SerializeField] protected float throwInterval = 0.25f;
    [Tooltip("落点沿垂直方向散开的距离，避免几颗砸在同一个点")]
    [SerializeField] protected float spreadDistance = 1.2f;
    protected float timeInAir;
    protected float timeSinceLastAttack;
    protected int throwableIndex = -1;

    /// <summary>
    /// 注册手雷投掷物的对象池。
    /// </summary>
    public override void Init(BossMonster monster, EntityManager entityManager, Character playerCharacter)
    {
        base.Init(monster, entityManager, playerCharacter);
        throwableIndex = entityManager.AddPoolForThrowable(grenadePrefab);
    }

    // void Update()
    // {
    //     if (active)
    //     {
    //         timeSinceLastAttack += Time.deltaTime;
    //         if (timeSinceLastAttack >= 1/fireRate)
    //         {
    //             timeSinceLastAttack = Mathf.Repeat(timeSinceLastAttack, 1/fireRate);
    //             LaunchGrenade();
    //         }
    //     }
    // }

    /// <summary>
    /// 技能激活期间持续朝玩家移动并同步网格位置。
    /// </summary>
    void FixedUpdate()
    {
        if (active)
        {
            Vector2 moveDirection = (playerCharacter.transform.position - monster.transform.position).normalized;
            monster.Move(moveDirection, Time.fixedDeltaTime);
            entityManager.Grid.UpdateClient(monster);
        }
    }

    /// <summary>
    /// 按预判落点投出一颗手雷，并按序号沿垂直方向散开落点。
    /// </summary>
    protected void LaunchGrenade(int index)
    {
        Throwable throwable = entityManager.SpawnThrowable(throwableIndex, monster.CenterTransform.position, damage, knockback, timeInAir, targetLayer);
        Vector2 targetPosition = playerCharacter.transform.position;
        targetPosition += playerCharacter.Velocity * throwable.ThrowTime;
        // 落点沿垂直于投掷方向散开：不散开的话三颗会砸在同一个点上，等于一次吃满三倍伤害
        Vector2 throwDirection = (targetPosition - (Vector2)monster.CenterTransform.position).normalized;
        Vector2 perpendicular = new Vector2(-throwDirection.y, throwDirection.x);
        float offsetIndex = index - (grenadeCount - 1) / 2f;
        targetPosition += perpendicular * (offsetIndex * spreadDistance);
        throwable.Throw(targetPosition);
    }

    /// <summary>
    /// 连续投出若干颗手雷，并扣掉已用时间，让整回合时长保持固定。
    /// </summary>
    protected IEnumerator LaunchGrenadeRoutine()
    {
        for (int i = 0; i < grenadeCount; i++)
        {
            LaunchGrenade(i);
            yield return new WaitForSeconds(throwInterval);
        }
        // 尾部冷却扣掉已经花掉的投掷时间，让整个回合长度不变。
        // 不扣的话单回合会长到 4s+，手雷会从「一个招式」变成 Boss 的主节奏。
        yield return new WaitForSeconds(Mathf.Max(0f, 1/fireRate - (grenadeCount - 1) * throwInterval));
    }

    /// <summary>
    /// 激活手雷技能并等待整轮投掷结束。
    /// </summary>
    public override IEnumerator Activate()
    {
        active = true;
        yield return StartCoroutine(LaunchGrenadeRoutine());
    }

    /// <summary>
    /// 单次投掷回合的总时长：投完所有手雷后补齐冷却。
    /// </summary>
    protected override float Duration
    {
        get
        {
            float throwPhase = grenadeCount * throwInterval;
            float tail = Mathf.Max(0f, 1f / Mathf.Max(0.01f, fireRate) - (grenadeCount - 1) * throwInterval);
            return Mathf.Max(0.01f, throwPhase + tail);
        }
    }

    /// <summary>
    /// 手雷是远程手段：贴脸时几乎不用，拉开距离后适配度拉满。
    /// </summary>
    public override float Score()
    {
        float distance = Vector2.Distance(monster.transform.position, playerCharacter.transform.position);
        // 2 单位内不投雷，5 单位外满适配。
        return ScoreByDuration(Mathf.Clamp01((distance - 2f) / 3f));
    }
}
