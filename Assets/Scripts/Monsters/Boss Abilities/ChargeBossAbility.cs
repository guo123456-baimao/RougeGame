using System.Reflection;
using System.Linq;
using System.Collections;
using UnityEngine;
public class ChargeBossAbility : BossAbility
{
    [Header("Charge Details")]
    [SerializeField] protected SpriteRenderer chargeIndicator;
    [SerializeField] protected Color defaultColor, warningColor;
    [SerializeField] protected float chargeUpTime;
    [SerializeField] protected float chargeDelay;
    [SerializeField] protected float chargeCooldown;
    [SerializeField] protected float chargeDistance;
    [SerializeField] protected float chargeSpeed;
    [SerializeField] protected float chargeCutoff = 0.8f;
    protected float timeSinceLastAttack;
    protected float radius;
    protected bool charging = false;

    /// <summary>
    /// 记录 Boss 碰撞体半径，供冲锋判定使用。
    /// </summary>
    public override void Init(BossMonster monster, EntityManager entityManager, Character playerCharacter)
    {
        base.Init(monster, entityManager, playerCharacter);
        CircleCollider2D bossCollider = monster.GetComponent<CircleCollider2D>();
        radius = bossCollider.radius;
    }

    // void Update()
    // {
    //     if (active)
    //     {
    //         timeSinceLastAttack += Time.deltaTime;
    //         if (timeSinceLastAttack >= 7)
    //         {
    //             timeSinceLastAttack = Mathf.Repeat(timeSinceLastAttack, 7);
    //             StartCoroutine(ChargeAttack());
    //         }
    //     }
    // }

    /// <summary>
    /// 未处于冲锋状态时持续朝玩家移动。
    /// </summary>
    void FixedUpdate()
    {
        if (active && !charging)
        {
            Vector2 moveDirection = (playerCharacter.transform.position - monster.transform.position).normalized;
            monster.Move(moveDirection, Time.fixedDeltaTime);
            entityManager.Grid.UpdateClient(monster);
        }
    }

    /// <summary>
    /// 冲锋技：绘制指示线蓄力、闪烁预警，随后沿路线冲刺并进入冷却。
    /// </summary>
    protected IEnumerator ChargeAttack()
    {
        charging = true;

        // 前摇并绘制冲锋指示线
        float t = 0;
        chargeIndicator.color = defaultColor;
        chargeIndicator.enabled = true;
        Vector2 direction = playerCharacter.transform.position - monster.transform.position;
        while (t < chargeUpTime)
        {
            direction = playerCharacter.transform.position - monster.transform.position;
            float length = t/chargeUpTime * chargeDistance;

            chargeIndicator.transform.localScale = new Vector3(length, chargeIndicator.transform.localScale.y, 1);
            chargeIndicator.transform.localRotation = Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.right, direction));
            chargeIndicator.transform.position = (Vector2) monster.transform.position + direction.normalized * t/chargeUpTime*chargeDistance/2;

            t += Time.deltaTime;
            yield return null;
        }

        // 闪烁警示
        chargeIndicator.color = warningColor;
        yield return new WaitForSeconds(chargeDelay/3);
        chargeIndicator.color = defaultColor;
        yield return new WaitForSeconds(chargeDelay/3);
        chargeIndicator.color = warningColor;
        yield return new WaitForSeconds(chargeDelay/3);
        chargeIndicator.enabled = false;

        // 沿冲锋路线移动
        t = 0;
        float CHARGE_FAILSAFE_TIME = 2;
        Vector2 initialPosition = monster.transform.position;
        float distance = 0;
        while (distance < chargeDistance*chargeCutoff && t < CHARGE_FAILSAFE_TIME)
        {
            monster.Move(direction*chargeSpeed, Time.deltaTime);
            distance = Vector2.Distance(monster.transform.position, initialPosition);

            t += Time.deltaTime;
            yield return null;
        }

        monster.Rigidbody.drag *= chargeSpeed/2;
        //monster.Animator.StopAnimating();
        yield return new WaitForSeconds(chargeCooldown);
        //monster.Animator.StartAnimating();
        monster.Rigidbody.drag /= chargeSpeed/2;

        charging = false;
    }

    /// <summary>
    /// 激活冲锋技能并等待整段动作播放完毕。
    /// </summary>
    public override IEnumerator Activate()
    {
        active = true;
        yield return StartCoroutine(ChargeAttack());
    }

    /// <summary>
    /// 单次冲锋的总时长：蓄力 + 闪烁预警 + 冲刺 + 收尾冷却。
    /// </summary>
    protected override float Duration =>
        Mathf.Max(0.01f, chargeUpTime + chargeDelay + chargeDistance * chargeCutoff / Mathf.Max(0.01f, chargeSpeed) + chargeCooldown);

    /// <summary>
    /// 距离玩家越远评分越高（越远冲锋收益越大）。
    /// </summary>
    public override float Score()
    {
        float distance = Vector2.Distance(monster.transform.position, playerCharacter.transform.position);
        // 冲锋需要助跑距离：1.5 单位内没法冲，4 单位外满适配。
        return ScoreByDuration(Mathf.Clamp01((distance - 1.5f) / 2.5f));
    }
}
