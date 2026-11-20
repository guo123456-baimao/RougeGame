using System.Collections;
using UnityEngine;
public class LifestealAbility : Ability
{
    [Header("Lifesteal Stats")]
    [SerializeField] protected UpgradeableRecovery recovery;
    [SerializeField] protected UpgradeableRecoveryChance recoveryChance;
    private float timeSinceLastHealed;

    /// <summary>
    /// 为玩家开启吸血效果。
    /// </summary>
    protected override void Use()
    {
        base.Use();
        playerCharacter.OnDealDamage.AddListener(PlayerDealtDamage);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 按恢复概率把造成的伤害转化为自身治疗。
    /// </summary>
    void PlayerDealtDamage(float damage)
    {
        if (Random.Range(0.0f, 1.0f) < recoveryChance.Value)
        {
            playerCharacter.GainHealth(recovery.Value);
        }
    }
}
