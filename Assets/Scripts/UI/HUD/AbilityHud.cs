using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// 局内技能栏：图标 + 冷却扇形 + 等级角标。
/// 场景里 "Abilities" 节点原本是个空的布局容器，这里按名字找到它并填满；
/// 找不到就在底部中间自己建一个。
/// </summary>
public class AbilityHud : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private RectTransform slotContainer;
    [SerializeField] private HorizontalLayoutGroup slotLayout;
    [SerializeField] private HudTheme theme;

    [Header("Auto Build")]
    [Tooltip("场景里已经预留的容器名（Level 1 的 Upper UI 下有一个空的 Abilities），找到了就直接用")]
    [SerializeField] private string containerSearchName = "Abilities";
    [SerializeField] private bool buildContainerIfMissing = true;
    [SerializeField] private Vector2 fallbackAnchor = new Vector2(0.5f, 0f);
    [SerializeField] private Vector2 fallbackOffset = new Vector2(0f, 110f);

    private AbilityManager abilityManager;
    private readonly List<AbilitySlot> slots = new List<AbilitySlot>();
    private readonly List<Ability> tracked = new List<Ability>();

    public HudTheme Theme { get => theme; set => theme = value; }

    private HudTheme ActiveTheme => theme != null ? theme : HudTheme.Active;

    /// <summary>
    /// 构建技能栏容器并准备槽位布局。
    /// </summary>
    private void Awake()
    {
        if (theme == null)
            theme = HudTheme.Active;

        if (slotContainer == null && !string.IsNullOrEmpty(containerSearchName))
            slotContainer = FindContainerByName();
        if (slotContainer == null && buildContainerIfMissing)
            BuildContainer();

        if (slotContainer != null)
        {
            slotLayout = slotContainer.GetComponent<HorizontalLayoutGroup>();
            if (slotLayout != null)
                slotLayout.spacing = ActiveTheme.slotSpacing;
            GridLayoutGroup grid = slotContainer.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.cellSize = new Vector2(ActiveTheme.slotSize, ActiveTheme.slotSize);
                grid.spacing = new Vector2(ActiveTheme.slotSpacing, 0f);
            }

            if (Application.isPlaying)
                ClearSampleSlots(); // 烘焙进场景的示例槽位只在编辑器里当样式预览，运行时清掉
        }
    }

    /// <summary>清掉烘焙时放进容器的示例槽位（名字以 Sample Slot 开头的）。</summary>
    private void ClearSampleSlots()
    {
        for (int i = slotContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = slotContainer.GetChild(i);
            if (child.name.StartsWith("Sample Slot"))
                Destroy(child.gameObject);
        }
    }

    /// <summary>在所属 Canvas 下按名字找预留容器，避免为了接一个引用去改场景文件。</summary>
    private RectTransform FindContainerByName()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform root = canvas != null ? canvas.transform : transform.root;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == containerSearchName)
                return child as RectTransform;
        }
        return null;
    }

    /// <summary>
    /// 销毁时移除技能变化监听。
    /// </summary>
    private void OnDestroy()
    {
        if (abilityManager != null)
            abilityManager.OnAbilitiesChanged -= Refresh;
    }

    /// <summary>
    /// 创建技能栏的布局容器。
    /// </summary>
    private void BuildContainer()
    {
        RectTransform root = HudUiFactory.CreateAnchored("Ability Slots", transform, fallbackAnchor,
            fallbackAnchor, new Vector2(400f, ActiveTheme.slotSize + 12f), fallbackOffset);
        slotContainer = root;
        slotLayout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        slotLayout.childAlignment = TextAnchor.MiddleCenter;
        slotLayout.childForceExpandWidth = false;
        slotLayout.childForceExpandHeight = false;
        slotLayout.childControlWidth = false;
        slotLayout.childControlHeight = false;
        slotLayout.spacing = ActiveTheme.slotSpacing;
    }

    /// <summary>
    /// 绑定技能管理器，并重建已有技能的槽位。
    /// </summary>
    public void Bind(AbilityManager abilityManager)
    {
        if (this.abilityManager != null)
            this.abilityManager.OnAbilitiesChanged -= Refresh;
        this.abilityManager = abilityManager;
        if (abilityManager != null)
            abilityManager.OnAbilitiesChanged += Refresh;
        Refresh();
    }

    /// <summary>
    /// 只增不减地把已拥有技能补齐。
    /// 之所以不按 ownedAbilities 全量重建：升级面板打开期间 AbilityManager 会把
    /// 候选技能临时移出 owned 列表，全量重建会让技能栏在选卡时闪空。
    /// </summary>
    public void Refresh()
    {
        if (abilityManager == null) return;

        for (int i = tracked.Count - 1; i >= 0; i--)
        {
            if (tracked[i] == null)
            {
                RemoveSlotAt(i);
                tracked.RemoveAt(i);
            }
        }

        foreach (Ability ability in abilityManager.OwnedAbilities)
        {
            if (ability == null || tracked.Contains(ability)) continue;
            tracked.Add(ability);
            slots.Add(CreateSlot(ability));
        }

        for (int i = 0; i < slots.Count; i++)
            slots[i].UpdateLevel();
    }

    /// <summary>
    /// 移除指定下标的技能槽位。
    /// </summary>
    private void RemoveSlotAt(int index)
    {
        if (index < 0 || index >= slots.Count) return;
        if (slots[index].Root != null)
            Destroy(slots[index].Root.gameObject);
        slots.RemoveAt(index);
    }

    /// <summary>
    /// 为指定技能创建槽位（图标、冷却遮罩与等级角标）。
    /// </summary>
    private AbilitySlot CreateSlot(Ability ability)
    {
        HudTheme t = ActiveTheme;
        RectTransform root = BuildSlotVisual(ability.Name, ability.Image, t,
            out Image icon, out Image cooldown, out RectTransform badgeRoot, out TextMeshProUGUI badgeText);

        AbilitySlot slot = new AbilitySlot(ability, root, icon, cooldown, badgeRoot, badgeText, t);
        slot.UpdateLevel();
        StartCoroutine(PopIn(root, t.slotPopDuration));
        return slot;
    }

    /// <summary>搭一个技能槽的完整可视层级（Frame/Inner/Icon/Cooldown/等级角标），运行时与编辑器烘焙共用。</summary>
    private RectTransform BuildSlotVisual(string slotName, Sprite iconSprite, HudTheme t,
        out Image icon, out Image cooldown, out RectTransform badgeRoot, out TextMeshProUGUI badgeText)
    {
        float size = t.slotSize;

        RectTransform root = HudUiFactory.CreateRect(slotName, slotContainer != null ? slotContainer : transform);
        root.sizeDelta = new Vector2(size, size);

        Image frame = HudUiFactory.CreateImage("Frame", root, t.slotFrame);
        HudUiFactory.Stretch(frame.rectTransform);

        float inset = Mathf.Max(2f, size * 0.045f);
        RectTransform inner = HudUiFactory.CreateRect("Inner", root);
        inner.anchorMin = Vector2.zero;
        inner.anchorMax = Vector2.one;
        inner.pivot = new Vector2(0.5f, 0.5f);
        inner.offsetMin = new Vector2(inset, inset);
        inner.offsetMax = new Vector2(-inset, -inset);

        Image background = HudUiFactory.CreateImage("Background", inner, t.slotBackground);
        HudUiFactory.Stretch(background.rectTransform);

        icon = HudUiFactory.CreateImage("Icon", inner, Color.white, iconSprite);
        HudUiFactory.Stretch(icon.rectTransform);
        icon.preserveAspect = true;
        icon.rectTransform.offsetMin = new Vector2(4f, 4f);
        icon.rectTransform.offsetMax = new Vector2(-4f, -4f);
        icon.enabled = iconSprite != null;

        cooldown = HudUiFactory.CreateFilledImage("Cooldown", root, t.cooldownMask, Image.FillMethod.Radial360);
        HudUiFactory.Stretch(cooldown.rectTransform);
        cooldown.fillAmount = 0f;

        // 等级角标
        float badge = size * 0.42f;
        badgeRoot = HudUiFactory.CreateAnchored("Level", root, new Vector2(1f, 0f),
            new Vector2(1f, 0f), new Vector2(badge, badge), new Vector2(-2f, 2f));
        Image badgeBg = HudUiFactory.CreateImage("Badge BG", badgeRoot, new Color(0f, 0f, 0f, 0.8f));
        HudUiFactory.Stretch(badgeBg.rectTransform);
        badgeText = HudUiFactory.CreateText("Badge Text", badgeRoot, "1",
            badge * 0.62f, t.textColor, TextAlignmentOptions.Midline, t.Font);
        HudUiFactory.Stretch(badgeText.rectTransform);

        return root;
    }

    /// <summary>
    /// 编辑器烘焙：把技能槽容器与两个示例槽位建进场景，供 Scene 视图直接调整样式与位置；
    /// 运行时 Awake 检测到容器已接好，会先清掉示例槽再按技能数据重建真实槽位。
    /// </summary>
    public void BuildForEditor()
    {
        if (Application.isPlaying) return;
        if (theme == null) theme = HudTheme.Active;

        if (slotContainer == null && !string.IsNullOrEmpty(containerSearchName))
            slotContainer = FindContainerByName();
        if (slotContainer == null && buildContainerIfMissing)
            BuildContainer();
        if (slotContainer == null) return;

        if (slotLayout == null)
            slotLayout = slotContainer.GetComponent<HorizontalLayoutGroup>();
        if (slotLayout != null)
            slotLayout.spacing = ActiveTheme.slotSpacing;
        GridLayoutGroup grid = slotContainer.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.cellSize = new Vector2(ActiveTheme.slotSize, ActiveTheme.slotSize);
            grid.spacing = new Vector2(ActiveTheme.slotSpacing, 0f);
        }

        // 两个示例槽位：纯样式预览，运行时会被 Awake 清掉
        HudTheme t = ActiveTheme;
        BuildSlotVisual("Sample Slot 1", null, t, out _, out _, out _, out _);
        BuildSlotVisual("Sample Slot 2", null, t, out _, out _, out _, out _);
    }

    /// <summary>
    /// 槽位出现动画：从 0 弹到 1。
    /// </summary>
    private System.Collections.IEnumerator PopIn(RectTransform rect, float duration)
    {
        float t = 0f;
        rect.localScale = Vector3.zero;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, duration);
            rect.localScale = Vector3.one * EasingUtils.EaseOutBack(Mathf.Clamp01(t));
            yield return null;
        }
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// 每帧刷新技能冷却与等级显示。
    /// </summary>
    private void Update()
    {
        for (int i = 0; i < slots.Count; i++)
            slots[i].UpdateCooldown();
    }

    private class AbilitySlot
    {
        private readonly Ability ability;
        private readonly Image icon;
        private readonly Image cooldown;
        private readonly RectTransform badge;
        private readonly TextMeshProUGUI badgeText;
        private readonly HudTheme theme;
        private int lastLevel = -1;
        private bool lastMaxed;

        public RectTransform Root { get; }
        public Ability Ability { get => ability; }

        /// <summary>
        /// 记录技能与其对应的界面元素引用。
        /// </summary>
        public AbilitySlot(Ability ability, RectTransform root, Image icon, Image cooldown, RectTransform badge,
            TextMeshProUGUI badgeText, HudTheme theme)
        {
            this.ability = ability;
            Root = root;
            this.icon = icon;
            this.cooldown = cooldown;
            this.badge = badge;
            this.badgeText = badgeText;
            this.theme = theme;
        }

        /// <summary>
        /// 按技能冷却进度刷新冷却遮罩的填充比例。
        /// </summary>
        public void UpdateCooldown()
        {
            if (ability == null)
            {
                if (cooldown != null) cooldown.fillAmount = 0f;
                return;
            }

            if (!ability.HasCooldown)
            {
                if (cooldown != null && cooldown.fillAmount != 0f) cooldown.fillAmount = 0f;
                return;
            }

            float progress = Mathf.Clamp01(ability.CooldownProgress);
            cooldown.fillAmount = 1f - progress;
            // 冷却刚转好时轻微提亮，给一个"可用了"的暗示
            if (icon != null)
                icon.color = progress >= 1f ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f);
        }

        /// <summary>
        /// 刷新等级角标显示。
        /// </summary>
        public void UpdateLevel()
        {
            if (ability == null) return;
            int level = ability.Level;
            bool maxed = !ability.RequirementsMet();
            if (level == lastLevel && maxed == lastMaxed) return;
            lastLevel = level;
            lastMaxed = maxed;

            if (badgeText != null)
            {
                badgeText.text = level.ToString();
                badgeText.color = maxed ? theme.coinColor : theme.textColor;
            }
            if (badge != null)
                badge.gameObject.SetActive(level > 1 || maxed);
        }
    }
}
