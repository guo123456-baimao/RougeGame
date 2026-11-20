using System.Collections;
using UnityEngine;
public class ShotgunBossAbility : BossAbility
{
    [Header("Shotgun Details")]
    [SerializeField] protected GameObject bulletPrefab;
    [SerializeField] protected LayerMask targetLayer;
    [SerializeField] protected float damage;
    [SerializeField] protected float knockback;
    [SerializeField] protected float bulletSpeedMin;
    [SerializeField] protected float bulletSpeedMax;
    [SerializeField] protected float fireRate;
    [SerializeField] protected float bulletCount;
    [SerializeField] protected float spreadAngle;
    [Tooltip("齐射前的停顿，给玩家一个看得见的预警")]
    [SerializeField] protected float telegraphTime = 0.35f;
    protected float timeSinceLastAttack;
    protected int projectileIndex = -1;

    /// <summary>
    /// 注册霰弹弹丸的对象池。
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
    /// 以扇形角度均匀发射霰弹，每颗速度在给定区间内随机。
    /// </summary>
    protected void LaunchBullets()
    {
        Vector2 initialDir = (playerCharacter.transform.position - monster.transform.position).normalized;
        for (int i = 0; i < bulletCount; i++)
        {
            float theta = (spreadAngle * i) / bulletCount - spreadAngle/2;
            Vector2 direction = new Vector2(
                initialDir.x * Mathf.Cos(Mathf.Deg2Rad * theta) - initialDir.y * Mathf.Sin(Mathf.Deg2Rad * theta),
                initialDir.x * Mathf.Sin(Mathf.Deg2Rad * theta) + initialDir.y * Mathf.Cos(Mathf.Deg2Rad * theta)
            );
            Projectile projectile = entityManager.SpawnProjectile(projectileIndex, monster.CenterTransform.position, damage, knockback, Random.Range(bulletSpeedMin, bulletSpeedMax), targetLayer);
            projectile.Launch(direction);
        }
    }

    /// <summary>
    /// 连续两轮齐射，每轮开火前留出可见的预警停顿。
    /// </summary>
    protected IEnumerator LaunchBulletsRoutine()
    {
        for (int i = 0; i < 2; i++)
        {
            // 开火前先停一下。原来齐射是瞬发的，贴身乱战里这一点停顿就是玩家
            // 「根本没注意到 Boss 放技能」的主要原因。
            yield return new WaitForSeconds(telegraphTime);
            LaunchBullets();
            yield return new WaitForSeconds(1/fireRate);
        }
    }

    /// <summary>
    /// 激活霰弹技能并等待两轮齐射结束。
    /// </summary>
    public override IEnumerator Activate()
    {
        active = true;
        yield return StartCoroutine(LaunchBulletsRoutine());
    }

    /// <summary>
    /// 单次两轮齐射的总时长，含每轮开火前的预警停顿。
    /// </summary>
    protected override float Duration => 2f * (telegraphTime + 1f / Mathf.Max(0.01f, fireRate));

    /// <summary>
    /// 贴身时适配度最高，距离越远越低，约 7 单位外不再开火。
    /// </summary>
    public override float Score()
    {
        float distance = Vector2.Distance(monster.transform.position, playerCharacter.transform.position);
        // 霰弹是贴身武器：3 单位内满适配，7 单位外完全不考虑，
        // 中间线性过渡，把远距离的出场时间让给冲锋和手雷。
        return ScoreByDuration(Mathf.Clamp01((7f - distance) / 4f));
    }
}
