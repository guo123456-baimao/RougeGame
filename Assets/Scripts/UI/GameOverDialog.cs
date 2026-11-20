using UnityEngine;
using TMPro;
public class GameOverDialog : DialogBox
{
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI coinsGained;
    [SerializeField] private TextMeshProUGUI enemiesRouted;
    [SerializeField] private TextMeshProUGUI damageDealt;
    [SerializeField] private TextMeshProUGUI damageTaken;
    [SerializeField] private GameObject background;

    /// <summary>
    /// 打开结算界面，按通关/失败展示对应标题与本局统计。
    /// </summary>
    public void Open(bool levelPassed, StatsManager statsManager)
    {
        statusText.text = levelPassed ? "通关" : "失败";
        coinsGained.text = "+" + statsManager.CoinsGained;
        enemiesRouted.text = statsManager.MonstersKilled.ToString();
        damageDealt.text = statsManager.DamageDealt.ToString();
        damageTaken.text = statsManager.DamageTaken.ToString();
        background.SetActive(true);
        base.Open();
    }

    

    /// <summary>
    /// 关闭结算界面。
    /// </summary>
    public override void Close()
    {
        AudioManager.Instance.PlayClick();
        CoinDisplayManager.UpdateAll();
        base.Close();
        background.SetActive(false);
    }

    /// <summary>
    /// 「返回主菜单」按钮的处理逻辑。
    /// </summary>
    public void ReturnToMainMenu()
    {
        var levelManager = FindObjectOfType<LevelManager>();
        if (levelManager != null)
            levelManager.ReturnToMainMenu();
    }

    /// <summary>
    /// 「重新开始」按钮的处理逻辑。
    /// </summary>
    public void RestartGame()
    {
        var levelManager = FindObjectOfType<LevelManager>();
        if (levelManager != null)
            levelManager.Restart();
    }
}
