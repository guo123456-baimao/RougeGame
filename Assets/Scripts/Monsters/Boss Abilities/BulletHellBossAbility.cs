using System.Collections;
using UnityEngine;
public class BulletHellBossAbility : BossAbility
{
    [Header("Bullet Hell Details")]
    [SerializeField] protected GameObject bulletPrefab;
    [SerializeField] protected LayerMask targetLayer;
    [SerializeField] protected float damage;
    [SerializeField] protected float knockback;
    [SerializeField] protected float bulletSpeed;
    [SerializeField] protected float fireRate;
    [SerializeField] protected float bulletCount;
    [Tooltip("一个回合放几圈弹幕")]
    [SerializeField] protected int ringCount = 2;
    [Tooltip("同一回合里每圈之间的间隔")]
    [SerializeField] protected float ringInterval = 0.35f;
    protected float timeSinceLastAttack;
    protected int projectileIndex = -1;

    /// <summary>
    /// 注册弹幕弹丸的对象池。
    /// </summary>
    public override void Init(BossMonster monster, EntityManager entityManager, Character playerCharacter)
    {
        base.Init(monster, entityManager, playerCharacter);
        projectileIndex = entityManager.AddPoolForProjectile(bulletPrefab);
    }

    // void Update()
    // {
    //     if (active)
    //     {
    //         timeSinceLastAttack += Time.deltaTime;
    //         if (timeSinceLastAttack >= 1/fireRate)
    //         {
    //             timeSinceLastAttack = Mathf.Repeat(timeSinceLastAttack, 1/fireRate);
    //             LaunchBullets();
    //         }
    //     }
    // }

    /// <summary>
    /// 技能激活期间持续朝玩家移动。
    /// </summary>
    void FixedUpdate()
    {
        if (active)
        {
            Vector2 moveDirection = (playerCharacter.transform.position - monster.transform.position).normalized;
            monster.Move(moveDirection, Time.fixedDeltaTime);
        }
    }

    /// <summary>
    /// 向四周均匀发射一圈弹幕。
    /// </summary>
    protected void LaunchBullets()
    {
        for (int i = 0; i < bulletCount; i++)
        {
            float theta = i * Mathf.PI * 2.0f / bulletCount;
            Vector2 direction = new Vector2(Mathf.Sin(theta), Mathf.Cos(theta));
            Projectile projectile = entityManager.SpawnProjectile(projectileIndex, monster.CenterTransform.position, damage, knockback, bulletSpeed, targetLayer);
            projectile.Launch(direction);
        }
    }

    /// <summary>
    /// 连续放出若干圈弹幕，并补齐剩余冷却，让整回合时长保持固定。
    /// </summary>
    protected IEnumerator LaunchBulletsRoutine()
    {
        for (int i = 0; i < ringCount; i++)
        {
            LaunchBullets();
            yield return new WaitForSeconds(ringInterval);
        }
        // 尾部补到 1/fireRate，让整个回合长度保持固定。
        // 时长必须和 Duration 完全一致，否则 Score() 算出的时间占比会失真。
        yield return new WaitForSeconds(Mathf.Max(0f, 1/fireRate - ringCount * ringInterval));
    }

    /// <summary>
    /// 激活弹幕技能并等待整轮弹幕放完。
    /// </summary>
    public override IEnumerator Activate()
    {
        active = true;
        yield return StartCoroutine(LaunchBulletsRoutine());
    }

    /// <summary>
    /// 结束弹幕技能并停止其协程。
    /// </summary>
    public override void Deactivate()
    {
        base.Deactivate();
    }

    /// <summary>
    /// 单次弹幕的实际回合长度：放完所有圈后补齐冷却，取两者较大值。
    /// </summary>
    protected override float Duration
    {
        get
        {
            float ringPhase = ringCount * ringInterval;
            float tail = Mathf.Max(0f, 1f / Mathf.Max(0.01f, fireRate) - ringPhase);
            return Mathf.Max(0.01f, ringPhase + tail);
        }
    }

    /// <summary>
    /// 弹幕没有距离门槛，任何时刻都能用，适配度恒为 1。
    /// </summary>
    public override float Score()
    {
        return ScoreByDuration(1f);
    }
}
