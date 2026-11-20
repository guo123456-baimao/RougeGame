using System.Collections;
using UnityEngine;
public class RecoveryAbility : Ability
{
    [Header("Recovery Stats")]
    [SerializeField] protected UpgradeableRecovery recovery;
    [SerializeField] protected UpgradeableRecoveryCooldown cooldown;
    private float timeSinceLastHealed;

    public override bool HasCooldown { get => true; }
    public override float CooldownProgress
    {
        get => cooldown.Value > 0f ? Mathf.Clamp01(timeSinceLastHealed / cooldown.Value) : 1f;
    }

    /// <summary>
    /// 立即回复一定量的生命。
    /// </summary>
    protected override void Use()
    {
        base.Use();
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 按冷却周期为玩家回复生命。
    /// </summary>
    void Update()
    {
        timeSinceLastHealed += Time.deltaTime;
        if (timeSinceLastHealed >= cooldown.Value)
        {
            playerCharacter.GainHealth(recovery.Value);
            timeSinceLastHealed = Mathf.Repeat(timeSinceLastHealed, cooldown.Value);
        }
    }
}
