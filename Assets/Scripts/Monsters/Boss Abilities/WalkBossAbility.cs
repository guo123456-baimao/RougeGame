using System.Reflection;
using System.Linq;
using System.Collections;
using UnityEngine;
public class WalkBossAbility : BossAbility
{
    [SerializeField] protected float walkTime;

    /// <summary>
    /// 技能激活期间朝玩家移动并同步网格位置。
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
    /// 行走固定时长后结束技能。
    /// </summary>
    public override IEnumerator Activate()
    {
        active = true;
        yield return new WaitForSeconds(walkTime);
    }

    /// <summary>
    /// 行走固定时长，作为时间占比计算的基准时长。
    /// </summary>
    protected override float Duration => Mathf.Max(0.01f, walkTime);

    /// <summary>
    /// 走位是永远可用的填充动作，适配度取 0.75 而不是 1：
    /// 小 Boss 只挂了 3 个攻击技能，若取 1，贴身且冲锋不适用的回合里
    /// 走位会占到 45%，看起来像在绕圈不出手。
    /// </summary>
    public override float Score()
    {
        return ScoreByDuration(0.75f);
    }
}
