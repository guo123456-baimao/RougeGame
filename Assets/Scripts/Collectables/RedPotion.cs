using System.Collections;
using UnityEngine;
public class RedPotion : Collectable
{
    [SerializeField] protected float healAmount = 50;
    [SerializeField] protected float healTime = 30;

    protected override string PickupLabel => "+" + Mathf.RoundToInt(healAmount) + " HP";
    protected override Color PickupColor => HudTheme.Active.healColor;

    /// <summary>
    /// 拾取后隐藏贴图，并启动持续回血流程。
    /// </summary>
    protected override void OnCollected()
    {
        gameObject.SetActive(true);
        GetComponentInChildren<SpriteRenderer>().enabled = false;
        AudioManager.Instance.PlayHealthPotion();
        StartCoroutine(HealOverTime());
    }

    /// <summary>
    /// 在 healTime 内按 healAmount 匀速回复生命，结束后销毁自身。
    /// </summary>
    private IEnumerator HealOverTime()
    {
        float t = 0;
        while (t < healTime)
        {
            t += Time.deltaTime;
            playerCharacter.GainHealth(Time.deltaTime * healAmount / healTime);
            yield return null;
        }
        Destroy(gameObject);
    }
}
