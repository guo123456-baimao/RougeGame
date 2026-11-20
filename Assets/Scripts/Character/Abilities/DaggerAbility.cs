using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
public class DaggerAbility : StabAbility
{
    [Header("Dagger Stats")]
    [SerializeField] protected UpgradeableBleedDamage bleedDamage;
    [SerializeField] protected UpgradeableBleedRate bleedRate;
    [SerializeField] protected UpgradeableBleedDuration bleedDuration;

    public class ActiveBleed
    {
        public Monster monster;
        public UnityAction<Monster> onKilled;
        public Coroutine routine;

        /// <summary>
        /// 记录被施加流血的怪物与其击杀回调。
        /// </summary>
        public ActiveBleed(Monster monster, UnityAction<Monster> onKilled)
        {
            this.monster = monster;
            this.onKilled = onKilled;
        }
    }

    private readonly List<ActiveBleed> activeBleeds = new List<ActiveBleed>();

    /// <summary>
    /// 结算匕首伤害，并为该怪物附加流血。
    /// </summary>
    protected override void DamageMonster(Monster monster, float damage, Vector2 knockback)
    {
        base.DamageMonster(monster, damage, knockback);
        StartBleed(monster);
    }

    /// <summary>
    /// 为目标创建流血状态并启动流血协程。
    /// </summary>
    private void StartBleed(Monster monster)
    {
        ActiveBleed bleed = null;
        UnityAction<Monster> onKilled = delegate { StopMonsterBleed(bleed); };
        bleed = new ActiveBleed(monster, onKilled);
        activeBleeds.Add(bleed);
        monster.OnKilled.AddListener(onKilled);
        bleed.routine = StartCoroutine(BleedMonster(monster, bleed));
    }

    /// <summary>
    /// 按间隔对目标造成流血伤害，目标死亡或状态结束时清理。
    /// </summary>
    protected IEnumerator BleedMonster(Monster monster, ActiveBleed bleed)
    {
        float bleedDelay = 1/bleedRate.Value;
        int bleedCount = Mathf.RoundToInt(bleedDuration.Value * bleedRate.Value);
        for (int i = 0; i < bleedCount; i++)
        {
            yield return new WaitForSeconds(bleedDelay);
            monster.TakeDamage(bleedDamage.Value);
            playerCharacter.OnDealDamage.Invoke(bleedDamage.Value);
            if (monster.HP <= 0)
                break;
        }
        EndBleed(bleed);
    }

    /// <summary>
    /// 结束流血状态并从状态表中移除。
    /// </summary>
    private void EndBleed(ActiveBleed bleed)
    {
        if (bleed == null) return;
        activeBleeds.Remove(bleed);
        if (bleed.monster != null && bleed.onKilled != null)
            bleed.monster.OnKilled.RemoveListener(bleed.onKilled);
    }

    /// <summary>
    /// 停止怪物身上的流血协程。
    /// </summary>
    protected void StopMonsterBleed(ActiveBleed bleed)
    {
        if (bleed == null) return;
        EndBleed(bleed);
        // 玩家死亡后怪物再死亡时，技能可能已经被销毁
        if (this == null || bleed.routine == null)
            return;
        StopCoroutine(bleed.routine);
    }

    /// <summary>
    /// 技能销毁时清空所有流血监听，避免悬挂引用。
    /// </summary>
    private void OnDestroy()
    {
        // 怪物会复用对象池，因此必须移除监听，避免它们继续指向已销毁的技能
        foreach (var bleed in activeBleeds)
        {
            if (bleed.monster != null && bleed.onKilled != null)
                bleed.monster.OnKilled.RemoveListener(bleed.onKilled);
        }
        activeBleeds.Clear();
    }
}
