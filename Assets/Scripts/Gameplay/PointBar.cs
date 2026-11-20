using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
public class PointBar : MonoBehaviour
{
    // 条与文字的滚动时长
    protected const float RollDuration = 0.35f;

    [SerializeField] protected RectTransform barBackground, barFill;
    // 可选：挂了就在条上显示「当前/上限」具体数值（如 10/50），不挂则只显示条
    [SerializeField] protected TextMeshProUGUI valueText;
    [SerializeField] protected UnityEvent onEmpty, onFull;

    [Header("Juice")]
    [Tooltip("受伤时主条先掉，后面留一条浅色延迟条再追上来，能看出这一下掉了多少")]
    [SerializeField] protected bool useLagBar = true;
    [Tooltip("低于阈值时条的填充色来回脉冲（血量条建议开，经验条别开）")]
    [SerializeField] protected bool pulseWhenLow = false;

    protected float currentPoints, minPoints, maxPoints;
    protected bool clamp;

    // displayedPoints：当前画出来的值（补间的当前帧）
    // currentPoints：目标值，CheckPoints 用它判断满/空，绝不能换成 displayedPoints
    protected float displayedPoints;
    protected Coroutine rollRoutine;

    private RectTransform lagFill;
    private Image fillImage;
    private Color baseFillColor = Color.white;
    private Color lagColor = Color.yellow;
    private float lagPoints;
    private float lagDelayTimer;
    private float pulseTime;
    private string lastValueText = string.Empty;

    public float CurrentPoints { get => currentPoints; set => currentPoints = value; }

    /// <summary>
    /// 缓存填充色、由主条颜色推导延迟条颜色，必要时动态构建延迟条。
    /// </summary>
    protected virtual void Awake()
    {
        if (barFill != null)
            fillImage = barFill.GetComponent<Image>();
        if (fillImage != null)
        {
            baseFillColor = fillImage.color;
            // 延迟条颜色直接由主条颜色推导，血量条/经验条各自成色，不用额外配
            lagColor = Color.Lerp(baseFillColor, Color.yellow, 0.55f);
        }
        if (useLagBar && lagFill == null && barFill != null)
            BuildLagBar();
    }

    /// <summary>
    /// 按主条的位置与尺寸创建一个浅色延迟条，并压在主条下方。
    /// </summary>
    private void BuildLagBar()
    {
        GameObject go = new GameObject("Lag Fill", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(barFill.parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = barFill.anchorMin;
        rect.anchorMax = barFill.anchorMax;
        rect.pivot = barFill.pivot;
        rect.anchoredPosition = barFill.anchoredPosition;
        rect.sizeDelta = barFill.sizeDelta;
        // 压在主条下面（SiblingIndex 越小越先绘制，也就越靠后）
        go.transform.SetSiblingIndex(barFill.GetSiblingIndex());

        Image lagImage = go.GetComponent<Image>();
        lagImage.sprite = fillImage != null ? fillImage.sprite : null;
        lagImage.color = lagColor;
        lagImage.raycastTarget = false;
        lagFill = rect;
    }

    /// <summary>
    /// 初始化数值并直接落位（不做补间），避免开局看到从 0 涨上来的假动画。
    /// </summary>
    public void Setup(float currentPoints, float minPoints, float maxPoints, bool clamp = true)
    {
        this.currentPoints = currentPoints;
        this.minPoints = minPoints;
        this.maxPoints = maxPoints;
        this.clamp = clamp;

        // 初始化不补间：条与文字直接落位，否则开局会先看到一段从 0 涨上来的假动画
        StopRoll();
        displayedPoints = currentPoints;
        lagPoints = currentPoints;
        lagDelayTimer = 0f;
        UpdateDisplay();
        SetLagWidth();
    }

    /// <summary>
    /// 增加点数，检查满/空事件并启动补间滚动。
    /// </summary>
    public void AddPoints(float points)
    {
        currentPoints += points;
        CheckPoints();
        StartRoll();
    }

    /// <summary>
    /// 减少点数，检查满/空事件并启动补间滚动。
    /// </summary>
    public void SubtractPoints(float points)
    {
        currentPoints -= points;
        CheckPoints();
        StartRoll();
    }

    /// <summary>
    /// 直接设置点数，检查满/空事件并启动补间滚动。
    /// </summary>
    public void SetPoints(float points)
    {
        currentPoints = points;
        CheckPoints();
        StartRoll();
    }

    /// <summary>
    /// 开关「低值脉冲」效果，关闭时把填充色还原为基础色。
    /// </summary>
    public void SetPulseWhenLow(bool value)
    {
        pulseWhenLow = value;
        if (!value && fillImage != null)
            fillImage.color = baseFillColor;
    }

    /// <summary>
    /// 按当前显示值刷新主条宽度与数值文本。
    /// </summary>
    public void UpdateDisplay()
    {
        if (barFill != null && barBackground != null && maxPoints > minPoints)
            barFill.sizeDelta = new Vector2(BarWidth * (displayedPoints - minPoints)/(maxPoints - minPoints), barFill.sizeDelta.y);
        UpdateValueText();
    }

    private float BarWidth => barBackground != null ? barBackground.rect.width : 0f;

    /// <summary>
    /// 按延迟条数值刷新延迟条宽度。
    /// </summary>
    private void SetLagWidth()
    {
        if (lagFill == null || barBackground == null || maxPoints <= minPoints) return;
        lagFill.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01((lagPoints - minPoints)/(maxPoints - minPoints)), lagFill.sizeDelta.y);
    }

    /// <summary>
    /// 刷新「当前/上限」文本，值未变化时跳过写入以省去 TMP 文本重建。
    /// </summary>
    private void UpdateValueText()
    {
        if (valueText == null) return;

        if (maxPoints <= minPoints)
        {
            if (lastValueText != string.Empty)
            {
                valueText.text = string.Empty;
                lastValueText = string.Empty;
            }
            return;
        }

        int current = Mathf.Max(0, Mathf.CeilToInt(displayedPoints));
        int max = Mathf.CeilToInt(maxPoints);
        string text = current + "/" + max;
        // 补间每帧都会走到这里，值没变就别写 TMP，省掉一次文本重建
        if (text == lastValueText) return;
        lastValueText = text;
        valueText.text = text;
    }

    /// <summary>
    /// 每帧更新延迟条与低值脉冲效果。
    /// </summary>
    private void Update()
    {
        if (useLagBar && lagFill != null)
            UpdateLagBar();
        if (pulseWhenLow)
            UpdatePulse();
    }

    /// <summary>
    /// 延迟条追赶逻辑：掉值时先停顿再追上，涨值时立即同步。
    /// </summary>
    private void UpdateLagBar()
    {
        HudTheme theme = HudTheme.Active;
        float dt = Time.unscaledDeltaTime;

        if (displayedPoints < lagPoints)
        {
            // 掉血：先顿一下再追，制造"刚才掉了这么多"的对比
            if (lagDelayTimer > 0f)
                lagDelayTimer -= dt;
            else
                lagPoints = Mathf.MoveTowards(lagPoints, displayedPoints,
                    dt * theme.lagBarSpeed * Mathf.Max(1f, maxPoints - minPoints));
        }
        else
        {
            // 回血/涨经验：延迟条立刻跟上，别让玩家以为还在掉
            lagDelayTimer = theme.lagBarDelay;
            lagPoints = displayedPoints;
        }

        SetLagWidth();
    }

    /// <summary>
    /// 数值低于阈值时让填充色做正弦脉冲，回升后还原。
    /// </summary>
    private void UpdatePulse()
    {
        if (fillImage == null || maxPoints <= minPoints) return;

        HudTheme theme = HudTheme.Active;
        float ratio = Mathf.Clamp01((displayedPoints - minPoints) / (maxPoints - minPoints));
        if (ratio < theme.lowHealthThreshold)
        {
            pulseTime += Time.unscaledDeltaTime * theme.lowHealthPulseSpeed;
            float k = 0.5f + 0.5f * Mathf.Sin(pulseTime);
            fillImage.color = Color.Lerp(baseFillColor, theme.warningColor, 0.3f + 0.5f * k);
        }
        else if (fillImage.color != baseFillColor)
        {
            pulseTime = 0f;
            fillImage.color = baseFillColor;
        }
    }

    // ===== 补间 =====

    /// <summary>
    /// 启动补间滚动；对象不可见时直接落位，避免停在旧值。
    /// </summary>
    private void StartRoll()
    {
        StopRoll();

        // 对象不可见时协程跑不起来，直接落位，避免激活后停在旧值
        if (!gameObject.activeInHierarchy)
        {
            displayedPoints = currentPoints;
            lagPoints = currentPoints;
            UpdateDisplay();
            SetLagWidth();
            return;
        }

        lagDelayTimer = HudTheme.Active.lagBarDelay;
        rollRoutine = StartCoroutine(RollRoutine());
    }

    /// <summary>
    /// 停止正在进行的补间滚动。
    /// </summary>
    private void StopRoll()
    {
        if (rollRoutine != null)
        {
            StopCoroutine(rollRoutine);
            rollRoutine = null;
        }
    }

    /// <summary>
    /// 在固定时长内把显示值补间到目标值，结束时对齐并清除浮点误差。
    /// </summary>
    private IEnumerator RollRoutine()
    {
        float from = displayedPoints;
        float to = currentPoints;
        float t = 0f;

        while (t < 1f)
        {
            // 必须用 unscaledDeltaTime：死亡/暂停时 timeScale = 0，
            // 用 deltaTime 会把条冻在半路
            t += Time.unscaledDeltaTime / RollDuration;
            displayedPoints = Mathf.Lerp(from, to, Mathf.Clamp01(t));
            UpdateDisplay();
            yield return null;
        }

        displayedPoints = to;
        UpdateDisplay();

        // 收尾对齐：把浮点累积误差清掉，让目标值与画出来的值完全一致
        currentPoints = displayedPoints;
        rollRoutine = null;
    }

    /// <summary>
    /// 检查点数是否达到上限/下限，触发对应事件并按需夹紧数值。
    /// </summary>
    private void CheckPoints()
    {
        if (currentPoints >= maxPoints)
        {
            onFull.Invoke();
            if (clamp)
                currentPoints = maxPoints;
        }
        else if (currentPoints <= minPoints)
        {
            onEmpty.Invoke();
            if (clamp)
                currentPoints = minPoints;
        }
    }
}
