using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// 关卡进度条：把 levelTime / levelBlueprint.levelTime 暴露给玩家，
/// 并在 miniBoss 刷新时间点打一个刻度标记，让"还要熬多久"变成可见信息。
/// </summary>
public class LevelProgressBar : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private HudTheme theme;

    [Header("Auto Build")]
    [SerializeField] private bool buildIfMissing = true;
    [SerializeField] private Vector2 barSize = new Vector2(420f, 10f);
    [Tooltip("相对画布顶部的偏移。Upper UI 那条 100px 的横条到 -112 为止，进度条放在它下面")]
    [SerializeField] private float anchorY = -124f;

    [Header("Runtime (auto build)")]
    [SerializeField] private RectTransform barRoot;
    [SerializeField] private RectTransform barTrack;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image markerImage;
    [SerializeField] private TextMeshProUGUI labelText;

    private float levelTime = 1f;
    private float miniBossTime = -1f;
    private string lastLabel = string.Empty;
    private bool bossPhase;
    // 三段式相位：phases 配置无效时为 null，走原单段回退显示
    private float[] phaseStartTimes;
    private string[] phaseNames;
    private Color[] phaseTints;      // 每个相位的 HUD 强调色（矿区暖黄/血色地暗红/虚空幽紫）
    private Image[] phaseHighlights;
    private RectTransform phaseMarksRoot;

    private HudTheme ActiveTheme => theme != null ? theme : HudTheme.Active;

    /// <summary>
    /// 取当前主题，必要时自动搭建进度条。
    /// </summary>
    private void Awake()
    {
        if (theme == null)
            theme = HudTheme.Active;

        if (buildIfMissing && barRoot == null)
            Build();
    }

    /// <summary>
    /// 搭建进度条的底槽、填充、时间文本与相位刻度容器。
    /// </summary>
    private void Build()
    {
        HudTheme t = ActiveTheme;
        float height = barSize.y + 20f;

        barRoot = HudUiFactory.CreateAnchored("Level Progress", transform, new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(barSize.x, height), new Vector2(0f, anchorY));

        barTrack = HudUiFactory.CreateAnchored("Track", barRoot, new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), barSize, new Vector2(0f, -barSize.y * 0.5f));
        Image trackBg = HudUiFactory.CreateImage("Track BG", barTrack, t.trackColor);
        HudUiFactory.Stretch(trackBg.rectTransform);

        Image frame = HudUiFactory.CreateImage("Frame", barTrack, t.frameColor);
        HudUiFactory.Stretch(frame.rectTransform);
        frame.rectTransform.offsetMin = new Vector2(-2f, -2f);
        frame.rectTransform.offsetMax = new Vector2(2f, 2f);
        frame.transform.SetAsFirstSibling();

        fillImage = HudUiFactory.CreateImage("Fill", barTrack, t.progressFill);
        HudUiFactory.Stretch(fillImage.rectTransform);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillAmount = 0f;

        markerImage = HudUiFactory.CreateImage("Mini Boss Marker", barTrack, t.warningColor);
        RectTransform markerRect = markerImage.rectTransform;
        markerRect.anchorMin = new Vector2(0f, 0f);
        markerRect.anchorMax = new Vector2(0f, 1f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = new Vector2(3f, 0f);
        markerRect.offsetMin = new Vector2(-1.5f, -3f);
        markerRect.offsetMax = new Vector2(1.5f, 3f);
        markerImage.gameObject.SetActive(false);

        // 相位名 + 倒计时是玩家主要看的文字：亮色加大字号，不加描边（描边会显得字体粗糊）
        labelText = HudUiFactory.CreateText("Label", barRoot, string.Empty, 24f, t.textColor,
            TextAlignmentOptions.Center, t.Font);
        labelText.outlineWidth = 0f;
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.offsetMin = new Vector2(0f, 0f);
        labelRect.offsetMax = new Vector2(0f, 30f);
    }

    /// <summary>
    /// 绑定关卡蓝图，读取总时长并按相位配置生成刻度。
    /// </summary>
    public void Bind(LevelBlueprint levelBlueprint)
    {
        if (levelBlueprint == null) return;

        levelTime = Mathf.Max(1f, levelBlueprint.levelTime);
        ClearPhaseMarks();

        // 三段式：按相位配置画多段刻度；配置无效时回退原单段显示
        if (levelBlueprint.TryGetValidPhases(out float[] starts))
        {
            phaseStartTimes = starts;
            phaseNames = new string[starts.Length];
            phaseTints = new Color[starts.Length];
            for (int i = 0; i < starts.Length; i++)
            {
                phaseNames[i] = string.IsNullOrEmpty(levelBlueprint.phases[i].displayName)
                    ? $"Phase {i + 1}" : levelBlueprint.phases[i].displayName;
                phaseTints[i] = levelBlueprint.phases[i].labelTint;
            }
            BuildPhaseMarks(levelBlueprint);
        }
        else
        {
            miniBossTime = levelBlueprint.miniBosses != null && levelBlueprint.miniBosses.Length > 0
                ? levelBlueprint.miniBosses[0].spawnTime
                : -1f;

            if (markerImage != null)
            {
                bool hasMarker = miniBossTime > 0f && miniBossTime < levelTime;
                markerImage.gameObject.SetActive(hasMarker);
                if (hasMarker)
                    markerImage.rectTransform.anchoredPosition =
                        new Vector2(barSize.x * (miniBossTime / levelTime), 0f);
            }
        }
    }

    /// <summary>按相位重建动态刻度：当前段高亮、段间分隔线、每段小 Boss 标记。
    /// 调用方 Bind 已先做 ClearPhaseMarks，这里不能再清，否则会清掉刚赋值的 phaseStartTimes。</summary>
    private void BuildPhaseMarks(LevelBlueprint levelBlueprint)
    {
        if (markerImage != null)
            markerImage.gameObject.SetActive(false);  // 多段模式下用 marksRoot 里的标记

        HudTheme t = ActiveTheme;
        phaseMarksRoot = HudUiFactory.CreateRect("Phase Marks", barTrack);
        HudUiFactory.Stretch(phaseMarksRoot);

        // 当前段高亮：半透明覆盖层，SetTime 时按当前相位开关
        phaseHighlights = new Image[phaseStartTimes.Length];
        for (int i = 0; i < phaseStartTimes.Length; i++)
        {
            float startX = phaseStartTimes[i] / levelTime;
            float endX = (i + 1 < phaseStartTimes.Length ? phaseStartTimes[i + 1] : levelTime) / levelTime;
            Image highlight = HudUiFactory.CreateImage($"Phase Highlight {i}", phaseMarksRoot,
                new Color(t.warningColor.r, t.warningColor.g, t.warningColor.b, 0.18f));
            RectTransform hr = highlight.rectTransform;
            hr.anchorMin = new Vector2(startX, 0f);
            hr.anchorMax = new Vector2(endX, 1f);
            hr.offsetMin = Vector2.zero;
            hr.offsetMax = Vector2.zero;
            highlight.gameObject.SetActive(i == 0);
            phaseHighlights[i] = highlight;
        }

        // 段间分隔线
        for (int i = 1; i < phaseStartTimes.Length; i++)
        {
            float x = barSize.x * (phaseStartTimes[i] / levelTime);
            Image separator = HudUiFactory.CreateImage($"Phase Separator {i}", phaseMarksRoot, t.frameColor);
            RectTransform sr = separator.rectTransform;
            sr.anchorMin = new Vector2(0f, 0f);
            sr.anchorMax = new Vector2(0f, 1f);
            sr.pivot = new Vector2(0.5f, 0.5f);
            sr.sizeDelta = new Vector2(3f, 0f);
            sr.offsetMin = new Vector2(-1.5f, 0f);
            sr.offsetMax = new Vector2(1.5f, 0f);
            separator.rectTransform.anchoredPosition = new Vector2(x, 0f);
        }

        // 每段小 Boss 标记（miniBossSpawnTime 相对相位开始）
        for (int i = 0; i < levelBlueprint.phases.Length; i++)
        {
            LevelBlueprint.LevelPhase phase = levelBlueprint.phases[i];
            if (phase.phaseMiniBoss == null || phase.phaseMiniBoss.bossBlueprint == null) continue;
            float bossTime = phaseStartTimes[i] + phase.miniBossSpawnTime;
            if (bossTime <= 0f || bossTime >= levelTime) continue;
            Image marker = HudUiFactory.CreateImage($"Mini Boss Marker {i}", phaseMarksRoot, t.warningColor);
            RectTransform mr = marker.rectTransform;
            mr.anchorMin = new Vector2(0f, 0f);
            mr.anchorMax = new Vector2(0f, 1f);
            mr.pivot = new Vector2(0.5f, 0.5f);
            mr.sizeDelta = new Vector2(3f, 0f);
            mr.offsetMin = new Vector2(-1.5f, -3f);
            mr.offsetMax = new Vector2(1.5f, 3f);
            marker.rectTransform.anchoredPosition = new Vector2(barSize.x * (bossTime / levelTime), 0f);
        }
    }

    /// <summary>
    /// 清除已有相位刻度及其容器。
    /// </summary>
    private void ClearPhaseMarks()
    {
        phaseStartTimes = null;
        phaseNames = null;
        phaseTints = null;
        phaseHighlights = null;
        if (phaseMarksRoot != null)
        {
            Destroy(phaseMarksRoot.gameObject);
            phaseMarksRoot = null;
        }
    }

    /// <summary>当前时间落在哪个相位（超出最后一段时停在最后相位）。</summary>
    private int GetPhaseIndexAt(float t)
    {
        int index = phaseStartTimes.Length - 1;
        for (int i = 0; i < phaseStartTimes.Length - 1; i++)
        {
            if (t < phaseStartTimes[i + 1])
            {
                index = i;
                break;
            }
        }
        return index;
    }

    /// <summary>
    /// 只高亮当前所处相位的刻度。
    /// </summary>
    private void UpdatePhaseHighlight(int phaseIndex)
    {
        if (phaseHighlights == null) return;
        for (int i = 0; i < phaseHighlights.Length; i++)
        {
            if (phaseHighlights[i] != null && phaseHighlights[i].gameObject.activeSelf != (i == phaseIndex))
                phaseHighlights[i].gameObject.SetActive(i == phaseIndex);
        }
    }

    /// <summary>
    /// 记录主题，供下次重建时使用。
    /// </summary>
    public void SetTheme(HudTheme newTheme)
    {
        theme = newTheme;
    }

    /// <summary>
    /// 编辑器烘焙：非运行时把进度条 UI 物体建进场景，Inspector / Scene 视图可直接调整；
    /// 运行时 Awake 检测到 barRoot 已存在就跳过 Build，直接接管烘焙结果。
    /// 相位刻度依赖关卡蓝图，仍由运行时 Bind 动态生成。
    /// </summary>
    public void BuildForEditor()
    {
        if (Application.isPlaying) return;
        if (theme == null) theme = HudTheme.Active;
        if (barRoot == null) Build();
    }

    /// <summary>
    /// 按已用时间刷新填充比例、时间文本与相位配色。
    /// </summary>
    public void SetTime(float t)
    {
        if (fillImage == null) return;

        float ratio = Mathf.Clamp01(t / levelTime);
        fillImage.fillAmount = ratio;
        bossPhase = t >= levelTime;

        if (labelText == null) return;

        // 相位强调色渐变：每帧向目标色靠拢（相位切换时 ~0.4s 内渐变到新颜色），不依赖文本变化
        Color targetColor = GetLabelColor(t);
        if (labelText.color != targetColor)
            labelText.color = Color.Lerp(labelText.color, targetColor, Mathf.Min(1f, Time.deltaTime * 6f));

        string label;
        if (bossPhase)
        {
            label = ActiveTheme.bossLabel;
        }
        else if (phaseStartTimes != null)
        {
            // 三段式：标签显示当前相位名 + 相位内剩余时间，并高亮当前段
            int phaseIndex = GetPhaseIndexAt(t);
            UpdatePhaseHighlight(phaseIndex);
            float phaseEnd = phaseIndex + 1 < phaseStartTimes.Length ? phaseStartTimes[phaseIndex + 1] : levelTime;
            label = phaseNames[phaseIndex] + " " + FormatTime(Mathf.Max(0f, phaseEnd - t));
        }
        else
        {
            label = ActiveTheme.timeToBossLabel + FormatTime(Mathf.Max(0f, levelTime - t));
        }
        if (label == lastLabel) return;
        lastLabel = label;
        labelText.text = label;
    }

    /// <summary>标签目标色：大 Boss 阶段用警示红，三段式用当前相位强调色，否则用主题亮色。</summary>
    private Color GetLabelColor(float t)
    {
        if (bossPhase) return ActiveTheme.warningColor;
        if (phaseTints != null && phaseTints.Length > 0)
            return phaseTints[GetPhaseIndexAt(t)];
        return ActiveTheme.textColor;
    }

    /// <summary>
    /// 把秒数格式化成 mm:ss。
    /// </summary>
    private static string FormatTime(float seconds)
    {
        TimeSpan span = TimeSpan.FromSeconds(seconds);
        return span.ToString(@"mm\:ss");
    }
}
