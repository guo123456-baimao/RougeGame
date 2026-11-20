using UnityEngine;
public class Health : Collectable
{
    [SerializeField] protected float healAmount = 30;

    protected override string PickupLabel => "+" + Mathf.RoundToInt(healAmount) + " HP";
    protected override Color PickupColor => HudTheme.Active.healColor;

    /// <summary>
    /// 立即回复固定生命值并销毁自身。
    /// </summary>
    protected override void OnCollected()
    {
        playerCharacter.GainHealth(healAmount);
        AudioManager.Instance.PlayHealthPotion();
        Destroy(gameObject);
    }
}
