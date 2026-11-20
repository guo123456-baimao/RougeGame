using System.Collections;
using UnityEngine;
using TMPro;
[RequireComponent(typeof(TextMeshProUGUI))]
public class CoinDisplay : MonoBehaviour
{
    // 与商店页 UITK 的金币滚动保持同一档节奏
    private const float RollDuration = 0.5f;

    private TextMeshProUGUI coinText;

    // displayedCoins：当前画出来的值；targetCoins：要滚到的值
    // displayedCoins < 0 表示还没落位（首次显示 / 对象不可见时补间跑不起来）
    private int displayedCoins = -1;
    private int targetCoins;
    private Coroutine rollRoutine;

    /// <summary>
    /// 缓存文本组件并读取当前金币数。
    /// </summary>
    void Awake()
    {
        coinText = GetComponent<TextMeshProUGUI>();
    }

    /// <summary>
    /// 面板/对话框打开时直接对齐到最新金币数：
    /// 打开瞬间滚一遍会让人以为金币刚变了，这里只做落位
    /// </summary>
    void OnEnable()
    {
        SetValueImmediate(DataStoreLocator.Store.GetCoins());
    }

    /// <summary>
    /// 从 IDataStore 对齐到最新值。已经落位且可见时走补间，
    /// 否则直接落位（对象不可见时协程跑不起来，硬等会永远停在旧数字）。
    /// </summary>
    public void Refresh()
    {
        int target = DataStoreLocator.Store.GetCoins();

        if (displayedCoins < 0 || !gameObject.activeInHierarchy)
        {
            SetValueImmediate(target);
            return;
        }

        // 目标没变：可能正在滚，不要打断
        if (target == targetCoins) return;

        targetCoins = target;
        StartRoll();
    }

    /// <summary>立刻把数字写成 value，不做补间（首次显示 / 对象不可见 / 初始化用）。</summary>
    public void SetValueImmediate(int value)
    {
        if (rollRoutine != null)
        {
            StopCoroutine(rollRoutine);
            rollRoutine = null;
        }

        displayedCoins = value;
        targetCoins = value;
        ApplyText();
    }

    /// <summary>
    /// 启动金币数字的滚动补间。
    /// </summary>
    private void StartRoll()
    {
        if (rollRoutine != null)
            StopCoroutine(rollRoutine);

        rollRoutine = StartCoroutine(RollRoutine());
    }

    /// <summary>
    /// 把显示值在固定时长内补间到目标值。
    /// </summary>
    private IEnumerator RollRoutine()
    {
        int from = displayedCoins;
        int to = targetCoins;
        float t = 0f;

        while (t < 1f)
        {
            // 必须用 unscaledDeltaTime：GameOver / 暂停时 timeScale = 0，
            // 用 deltaTime 会把数字冻在半路
            t += Time.unscaledDeltaTime / RollDuration;
            displayedCoins = Mathf.RoundToInt(Mathf.Lerp(from, to, Mathf.Clamp01(t)));
            ApplyText();
            yield return null;
        }

        displayedCoins = to;
        ApplyText();
        rollRoutine = null;
    }

    /// <summary>
    /// 把当前显示值写入文本。
    /// </summary>
    private void ApplyText()
    {
        if (coinText == null)
            coinText = GetComponent<TextMeshProUGUI>();
        if (coinText != null)
            coinText.text = displayedCoins.ToString();
    }
}

public static class CoinDisplayManager
{
    /// <summary>
    /// 批量对齐。刻意不在这里触发补间：FindObjectsOfType(includeInactive: true)
    /// 会把隐藏对象一起捞出来，作为补间触发点不可靠；
    /// 隐藏对象由 Refresh 内部自行落位，可见对象才滚。
    /// </summary>
    public static void UpdateAll()
    {
        var allDisplays = GameObject.FindObjectsOfType<CoinDisplay>(includeInactive: true);
        foreach (var d in allDisplays)
            d.Refresh();
    }
}
