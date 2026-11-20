using UnityEngine;
using TMPro;
[RequireComponent(typeof(TextMeshProUGUI))]
public class GameTimer : MonoBehaviour
{
    private TextMeshProUGUI timerText;
    private int lastSeconds = -1;

    /// <summary>
    /// 缓存 TMP 文本组件。
    /// </summary>
    void Awake()
    {
        timerText = GetComponent<TextMeshProUGUI>();
    }

    /// <summary>
    /// 每帧被 LevelManager 调用。秒数没变就不写 TMP：
    /// 原来每帧 new TimeSpan + ToString + 赋值，一秒 60 次文本重建，纯浪费。
    /// </summary>
    public void SetTime(float t)
    {
        int seconds = Mathf.Max(0, Mathf.FloorToInt(t));
        if (seconds == lastSeconds) return;
        lastSeconds = seconds;

        System.TimeSpan timeSpan = System.TimeSpan.FromSeconds(seconds);
        timerText.text = timeSpan.ToString(@"mm\:ss");
    }
}
