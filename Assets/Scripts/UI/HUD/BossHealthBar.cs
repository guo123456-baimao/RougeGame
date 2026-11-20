using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// Boss / 精英怪血条。原项目完全没有，玩家对 Boss 剩余血量毫无感知。
/// 主条即时掉血，后面叠一条延迟条，能直观看出这一发打掉了多少。
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private HudTheme theme;

    [Header("Auto Build")]
    [SerializeField] private bool buildIfMissing = true;
    [SerializeField] private Vector2 barSize = new Vector2(560f, 18f);
    [Tooltip("相对画布顶部的偏移，排在关卡进度条下面")]
    [SerializeField] private float anchorY = -168f;

    [Header("Runtime (auto build)")]
    [SerializeField] private RectTransform barRoot;
    [SerializeField] private RectTransform barTrack;
    [SerializeField] private Image lagImage;
    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI nameText;

    private Monster boss;
    private float maxHealth = 1f;
    private float lagRatio = 1f;
    private bool visible;
    private Coroutine appearRoutine;

    private HudTheme ActiveTheme => theme != null ? theme : HudTheme.Active;

    /// <summary>
    /// 缓存主题并创建血条节点。
    /// </summary>
    private void Awake()
    {
        if (theme == null)
            theme = HudTheme.Active;

        if (buildIfMissing && barRoot == null)
            Build();

        if (barRoot != null)
            barRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// 搭建 Boss 血条的底槽、填充与名称文本。
    /// </summary>
    private void Build()
    {
        HudTheme t = ActiveTheme;

        barRoot = HudUiFactory.CreateAnchored("Boss Health Bar", transform, new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(barSize.x, barSize.y + 26f), new Vector2(0f, anchorY));

        nameText = HudUiFactory.CreateText("Name", barRoot, t.bossLabel, 22f, t.textColor,
            TextAlignmentOptions.Center, t.Font);
        HudUiFactory.ApplyOutline(nameText, new Color(0f, 0f, 0f, 0.85f), 0.25f);
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.offsetMin = new Vector2(0f, -26f);
        nameRect.offsetMax = new Vector2(0f, 0f);

        barTrack = HudUiFactory.CreateAnchored("Track", barRoot, new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), barSize, Vector2.zero);
        Image trackBg = HudUiFactory.CreateImage("Track BG", barTrack, t.trackColor);
        HudUiFactory.Stretch(trackBg.rectTransform);

        Image frame = HudUiFactory.CreateImage("Frame", barTrack, t.frameColor);
        HudUiFactory.Stretch(frame.rectTransform);
        frame.rectTransform.offsetMin = new Vector2(-2f, -2f);
        frame.rectTransform.offsetMax = new Vector2(2f, 2f);
        frame.transform.SetAsFirstSibling();

        lagImage = HudUiFactory.CreateImage("Lag", barTrack, t.bossLag);
        HudUiFactory.Stretch(lagImage.rectTransform);
        lagImage.type = Image.Type.Filled;
        lagImage.fillMethod = Image.FillMethod.Horizontal;
        lagImage.fillAmount = 1f;

        fillImage = HudUiFactory.CreateImage("Fill", barTrack, t.bossFill);
        HudUiFactory.Stretch(fillImage.rectTransform);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillAmount = 1f;
    }

    /// <summary>
    /// 绑定 Boss 并显示血条。
    /// </summary>
    public void Show(Monster monster)
    {
        if (monster == null) return;

        boss = monster;
        maxHealth = Mathf.Max(1f, monster.MaxHealth);
        lagRatio = 1f;
        if (fillImage != null) fillImage.fillAmount = 1f;
        if (lagImage != null) lagImage.fillAmount = 1f;
        if (nameText != null) nameText.text = monster.DisplayName;

        if (barRoot == null) return;
        visible = true;
        barRoot.gameObject.SetActive(true);

        if (appearRoutine != null) StopCoroutine(appearRoutine);
        appearRoutine = StartCoroutine(Appear());
    }

    /// <summary>
    /// 隐藏 Boss 血条。
    /// </summary>
    public void Hide()
    {
        visible = false;
        boss = null;
        if (barRoot != null)
            barRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// 应用 HUD 主题配色。
    /// </summary>
    public void SetTheme(HudTheme newTheme)
    {
        theme = newTheme;
    }

    /// <summary>
    /// 编辑器烘焙：非运行时把血条 UI 物体建进场景，Inspector / Scene 视图可直接调整；
    /// 运行时 Awake 检测到 barRoot 已存在就跳过 Build，直接接管烘焙结果。
    /// </summary>
    public void BuildForEditor()
    {
        if (Application.isPlaying) return;
        if (theme == null) theme = HudTheme.Active;
        if (barRoot == null) Build();
    }

    /// <summary>
    /// 出现动画：淡入并轻微放大。
    /// </summary>
    private IEnumerator Appear()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, ActiveTheme.barAppearDuration);
            float e = EasingUtils.EaseOutBack(Mathf.Clamp01(t));
            if (barRoot != null)
                barRoot.localScale = new Vector3(e, e, 1f);
            yield return null;
        }
        if (barRoot != null)
            barRoot.localScale = Vector3.one;
    }

    /// <summary>
    /// 每帧刷新血量比例与血条位置。
    /// </summary>
    private void Update()
    {
        if (!visible || barRoot == null) return;

        if (boss == null || !boss.gameObject.activeInHierarchy)
        {
            Hide();
            return;
        }

        float ratio = Mathf.Clamp01(boss.HP / maxHealth);
        if (fillImage != null)
            fillImage.fillAmount = ratio;

        // 延迟条追赶：掉血时先停一下再快速追上，形成"扣了多少"的对比
        if (lagRatio > ratio)
            lagRatio = Mathf.MoveTowards(lagRatio, ratio, Time.unscaledDeltaTime * ActiveTheme.lagBarSpeed * 0.4f);
        else
            lagRatio = ratio;
        if (lagImage != null)
            lagImage.fillAmount = lagRatio;
    }
}
