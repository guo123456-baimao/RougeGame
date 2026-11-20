using System.Collections;
using UnityEngine;
public abstract class BossAbility : MonoBehaviour
{
    [Header("Ability Details")]
    protected BossMonster monster;
    protected EntityManager entityManager;
    protected Character playerCharacter;
    protected bool active = false;
    protected float useTime;

    /// <summary>
    /// 注入 Boss、实体管理器与玩家引用。
    /// </summary>
    public virtual void Init(BossMonster monster, EntityManager entityManager, Character playerCharacter)
    {
        this.monster = monster;
        this.entityManager = entityManager;
        this.playerCharacter = playerCharacter;
    }

    /// <summary>
    /// 执行该技能的一次完整动作（协程）。
    /// </summary>
    public abstract IEnumerator Activate();

    /// <summary>
    /// 关闭技能激活状态并停止所有协程。
    /// </summary>
    public virtual void Deactivate()
    {
        active = false;
        StopAllCoroutines();
    }

    /// <summary>
    /// 单次 Activate 占用的平均时长（秒），由各技能按自身节奏参数推算。
    /// </summary>
    protected abstract float Duration { get; }

    /// <summary>
    /// 返回该技能的评分，表示在当前时刻使用它有多合适
    /// </summary>
    public abstract float Score();

    /// <summary>
    /// 按动作时长折算评分：评分 = 适配度 / 时长。
    /// 选中概率与评分成正比，而占用时间又要乘以时长，两者相乘后
    /// 战斗时间占比只由适配度决定，动作越长权重自动越低，
    /// 无需再为手雷、霰弹这类长动作手动压低权重。
    /// </summary>
    protected float ScoreByDuration(float affinity)
    {
        return Mathf.Max(0f, affinity) / Mathf.Max(0.01f, Duration);
    }
}
