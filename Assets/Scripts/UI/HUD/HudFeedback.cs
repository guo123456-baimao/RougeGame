using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// 局内视觉反馈层：
/// - 受击时屏幕边缘泛红
/// - 低血量持续脉冲
/// - 升级时的暖色闪光
/// - 拾取飘字（金币 / 经验 / 回血），屏幕空间池化，避免世界空间 TMP 各画各的没法合批
/// </summary>
public class HudFeedback : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private HudTheme theme;

    [Header("Auto Build")]
    [SerializeField] private bool buildIfMissing = true;
    [SerializeField] private int pickupTextPoolSize = 16;

    [Header("Runtime (auto build)")]
    [SerializeField] private Image vignette;
    [SerializeField] private RectTransform pickupLayer;

    private Character playerCharacter;
    private Canvas parentCanvas;
    private readonly List<PickupItem> pickupItems = new List<PickupItem>();

    private float damageAlpha;
    private float levelUpAlpha;
    private float pulseTime;

    private HudTheme ActiveTheme => theme != null ? theme : HudTheme.Active;

    /// <summary>
    /// 缓存主题并创建受击遮罩与拾取飘字层。
    /// </summary>
    private void Awake()
    {
        if (theme == null)
            theme = HudTheme.Active;

        parentCanvas = GetComponentInParent<Canvas>();

        if (buildIfMissing && vignette == null)
            Build();
        else if (vignette != null && pickupLayer != null && pickupItems.Count == 0)
            ReusePickupPool(); // 场景里烘焙过拾取文字池：直接复用烘焙对象，编辑器里调的样式运行时也生效
    }

    /// <summary>
    /// 编辑器烘焙：非运行时把泛红遮罩与拾取文字池建进场景，Inspector / Scene 视图可直接调整；
    /// 运行时 Awake 检测到 vignette 已存在就跳过 Build，改为复用烘焙好的池子。
    /// </summary>
    public void BuildForEditor()
    {
        if (Application.isPlaying) return;
        if (theme == null) theme = HudTheme.Active;
        if (vignette == null)
        {
            pickupItems.Clear(); // 防重复入池
            Build();
        }
    }

    /// <summary>运行时接管场景里烘焙好的拾取文字池；池子数量不足时按原参数补齐。</summary>
    private void ReusePickupPool()
    {
        foreach (Transform child in pickupLayer)
        {
            if (child.name != "Pickup Text") continue;
            TextMeshProUGUI text = child.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
                pickupItems.Add(new PickupItem((RectTransform)child, text));
        }

        int target = Mathf.Max(1, pickupTextPoolSize);
        while (pickupItems.Count < target)
            pickupItems.Add(CreatePickupItem(ActiveTheme));
    }

    /// <summary>
    /// 构建受击全屏遮罩与飘字容器。
    /// </summary>
    private void Build()
    {
        HudTheme t = ActiveTheme;

        Image overlay = HudUiFactory.CreateImage("Damage Vignette", transform, Color.clear,
            HudUiFactory.CreateVignetteSprite());
        HudUiFactory.Stretch(overlay.rectTransform);
        overlay.transform.SetAsFirstSibling();
        vignette = overlay;

        pickupLayer = HudUiFactory.CreateRect("Pickup Texts", transform);
        HudUiFactory.Stretch(pickupLayer);
        pickupLayer.SetAsLastSibling();

        for (int i = 0; i < Mathf.Max(1, pickupTextPoolSize); i++)
            pickupItems.Add(CreatePickupItem(t));
    }

    /// <summary>
    /// 按主题创建一个拾取飘字条目。
    /// </summary>
    private PickupItem CreatePickupItem(HudTheme t)
    {
        RectTransform rect = HudUiFactory.CreateAnchored("Pickup Text", pickupLayer, new Vector2(0f, 0f),
            new Vector2(0.5f, 0.5f), new Vector2(200f, 40f), Vector2.zero);
        TextMeshProUGUI text = HudUiFactory.CreateText("Text", rect, string.Empty, 26f, t.textColor,
            TextAlignmentOptions.Center, t.Font);
        HudUiFactory.ApplyOutline(text, new Color(0f, 0f, 0f, 0.85f), 0.25f);
        HudUiFactory.Stretch(text.rectTransform);
        rect.gameObject.SetActive(false);
        return new PickupItem(rect, text);
    }

    /// <summary>
    /// 绑定角色，用于拾取飘字的世界坐标转换。
    /// </summary>
    public void Bind(Character character)
    {
        playerCharacter = character;
    }

    /// <summary>
    /// 应用 HUD 主题配色。
    /// </summary>
    public void SetTheme(HudTheme newTheme)
    {
        theme = newTheme;
    }

    /// <summary>
    /// 受击时闪一次全屏红色遮罩。
    /// </summary>
    public void FlashDamage()
    {
        damageAlpha = 1f;
    }

    /// <summary>
    /// 升级时闪一次高亮遮罩。
    /// </summary>
    public void FlashLevelUp()
    {
        levelUpAlpha = 1f;
    }

    /// <summary>
    /// 在世界坐标处弹出拾取飘字。
    /// </summary>
    public void ShowPickupText(string content, Color color, Vector3 worldPosition)
    {
        if (pickupItems.Count == 0 || pickupLayer == null) return;

        PickupItem item = null;
        for (int i = 0; i < pickupItems.Count; i++)
        {
            if (!pickupItems[i].Active)
            {
                item = pickupItems[i];
                break;
            }
        }
        // 全占满就抢最老的一个，宁可打断也不要丢掉反馈
        if (item == null)
            item = pickupItems[0];

        item.Show(content, color, WorldToLayerPosition(worldPosition));
    }

    /// <summary>
    /// 把世界坐标转换为 HUD 层内的局部坐标。
    /// </summary>
    private Vector2 WorldToLayerPosition(Vector3 worldPosition)
    {
        Camera camera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? parentCanvas.worldCamera
            : null;
        Vector3 screen = camera != null
            ? camera.WorldToScreenPoint(worldPosition)
            : (Camera.main != null ? Camera.main.WorldToScreenPoint(worldPosition) : worldPosition);

        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(pickupLayer, screen, camera, out local))
            return local;
        return Vector2.zero;
    }

    /// <summary>
    /// 每帧推进遮罩与飘字动画。
    /// </summary>
    private void Update()
    {
        UpdateOverlay();
        UpdatePickupTexts();
    }

    /// <summary>
    /// 按剩余时间淡出受击/升级遮罩。
    /// </summary>
    private void UpdateOverlay()
    {
        if (vignette == null) return;

        HudTheme t = ActiveTheme;
        float dt = Time.unscaledDeltaTime;

        if (damageAlpha > 0f)
            damageAlpha = Mathf.MoveTowards(damageAlpha, 0f, dt / Mathf.Max(0.0001f, t.damageFlashDuration));
        if (levelUpAlpha > 0f)
            levelUpAlpha = Mathf.MoveTowards(levelUpAlpha, 0f, dt / Mathf.Max(0.0001f, t.levelUpFlashDuration));

        float lowPulse = 0f;
        if (playerCharacter != null && playerCharacter.gameObject.activeInHierarchy)
        {
            float ratio = playerCharacter.HealthRatio;
            if (ratio < t.lowHealthThreshold && ratio > 0f)
            {
                pulseTime += dt * t.lowHealthPulseSpeed;
                float severity = 1f - ratio / t.lowHealthThreshold;
                lowPulse = severity * (0.12f + 0.10f * (0.5f + 0.5f * Mathf.Sin(pulseTime)));
            }
            else
            {
                pulseTime = 0f;
            }
        }

        float alpha = Mathf.Max(lowPulse, damageAlpha * 0.55f);
        Color color = t.damageVignette;
        if (levelUpAlpha > 0f && levelUpAlpha >= damageAlpha)
        {
            color = t.levelUpFlash;
            alpha = Mathf.Max(alpha, levelUpAlpha * 0.35f);
        }

        if (alpha <= 0.001f)
        {
            vignette.color = Color.clear;
            return;
        }
        color.a = Mathf.Clamp01(alpha);
        vignette.color = color;
    }

    /// <summary>
    /// 推进所有飘字的上升与淡出，结束后回收。
    /// </summary>
    private void UpdatePickupTexts()
    {
        if (pickupItems.Count == 0) return;

        HudTheme t = ActiveTheme;
        float duration = Mathf.Max(0.0001f, t.pickupTextDuration);
        for (int i = 0; i < pickupItems.Count; i++)
            pickupItems[i].Update(Time.unscaledDeltaTime / duration, t.pickupTextRise);
    }

    private class PickupItem
    {
        private readonly RectTransform rect;
        private readonly TextMeshProUGUI text;
        private float t;
        private Vector2 startPosition;
        private Color color = Color.white;

        public bool Active { get; private set; }

        /// <summary>
        /// 记录飘字的界面元素引用。
        /// </summary>
        public PickupItem(RectTransform rect, TextMeshProUGUI text)
        {
            this.rect = rect;
            this.text = text;
        }

        /// <summary>
        /// 显示飘字并重置其动画状态。
        /// </summary>
        public void Show(string content, Color color, Vector2 position)
        {
            t = 0f;
            startPosition = position;
            this.color = color;
            text.text = content;
            text.color = color;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
            rect.gameObject.SetActive(true);
            Active = true;
        }

        /// <summary>
        /// 推进单个飘字的上升与淡出动画。
        /// </summary>
        public void Update(float delta, float rise)
        {
            if (!Active) return;

            t += delta;
            if (t >= 1f)
            {
                rect.gameObject.SetActive(false);
                Active = false;
                return;
            }

            rect.anchoredPosition = startPosition + Vector2.up * rise * EasingUtils.EaseOutQuad(t);
            rect.localScale = Vector3.one * EasingUtils.EaseOutBack(Mathf.Clamp01(t * 4f));
            color.a = 1f - EasingUtils.EaseInQuad(t);
            text.color = color;
        }
    }
}
