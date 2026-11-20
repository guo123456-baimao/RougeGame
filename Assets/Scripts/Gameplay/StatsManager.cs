using UnityEngine;
using TMPro;
public class StatsManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI monstersKilledText;
    [SerializeField] private TextMeshProUGUI coinsGainedText;

    private int monstersKilled = 0;
    private float damageDealt = 0;
    private float damageTaken = 0;
    private int coinsGained = 0;

    public int MonstersKilled { get => monstersKilled; }
    public float DamageDealt { get => damageDealt; }
    public float DamageTaken { get => damageTaken; }
    public int CoinsGained { get => coinsGained; }

    /// <summary>
    /// 击杀数 +1 并刷新界面文本。
    /// </summary>
    public void IncrementMonstersKilled()
    {
        monstersKilled++;
        monstersKilledText.text = monstersKilled.ToString();
    }

    /// <summary>
    /// 累加获得的金币并刷新界面文本。
    /// </summary>
    public void IncreaseCoinsGained(int amount)
    {
        coinsGained += amount;
        coinsGainedText.text = coinsGained.ToString();
    }

    /// <summary>
    /// 累加造成的伤害总量，用于结算统计。
    /// </summary>
    public void IncreaseDamageDealt(float damage)
    {
        damageDealt += damage; 
    }

    /// <summary>
    /// 累加承受的伤害总量，用于结算统计。
    /// </summary>
    public void IncreaseDamageTaken(float damage)
    {
        damageTaken += damage;
    }
}
