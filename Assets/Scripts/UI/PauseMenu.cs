using UnityEngine;
using UnityEngine.UI;
public class PauseMenu : MonoBehaviour
{
    [SerializeField] private Image pauseButton;
    [SerializeField] private Sprite pauseSprite, playSprite;
    [SerializeField] private GameObject pauseMenu;
    private bool paused = false;
    private bool timeIsFrozen = false;

    public bool TimeIsFrozen { get => timeIsFrozen; set => timeIsFrozen = value; }
    public bool IsPaused => paused;

    /// <summary>
    /// 切换暂停/继续：控制时间流速与暂停界面的显隐。
    /// </summary>
    public void PlayPause()
    {
        AudioManager.Instance.PlayClick();
        paused = !paused;
        if (paused)
        {
            if (!timeIsFrozen)
                Time.timeScale = 0;
            pauseButton.sprite = playSprite;
            pauseMenu.SetActive(true);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PauseMachineGunLoop();
        }
        else
        {
            if (!timeIsFrozen)
                Time.timeScale = 1;
            pauseButton.sprite = pauseSprite;
            pauseMenu.SetActive(false);
            if (AudioManager.Instance != null)
                AudioManager.Instance.ResumeMachineGunLoop();
        }
    }
}
