using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
/// <summary>
/// UI Toolkit 版铭文强化页。数据源与 uGUI 版一致，都走 EngravingManager。
/// </summary>
public class EngravingPageUITKController : MonoBehaviour
{
    [SerializeField] private UIDocument document;
    [SerializeField] private EngravingBlueprint[] allEngravings;
    // 铭文卡结构来自 UXML 模板（Assets/UI/EngravingCard.uxml），
    // 卡片样式在 UI Builder 里改模板即可，无需改代码。
    [SerializeField] private VisualTreeAsset cardTemplate;

#if UNITY_EDITOR
    /// <summary>
    /// 模板引用留空时自动补上，省去每个场景手动拖一次
    /// </summary>
    private void OnValidate()
    {
        if (cardTemplate == null)
            cardTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/EngravingCard.uxml");
    }
#endif

    private const string SlotInfoDefault = "点击槽位可卸下铭文";

    // 拒绝操作时的负反馈：胶囊红闪 + 面板抖动，与商店页保持同一套手感
    private const float AlertDuration = 1.2f;
    private const float BlinkHalfPeriod = 0.08f;

    private class CardView
    {
        public EngravingBlueprint engraving;
        public VisualElement root;
        public Image icon;
        public Label name;
        public Label state;
        public Label effect;
        public Label curse;
        public Button action;
    }

    private class SlotView
    {
        public Button root;
        public Image icon;
        public string tooltipText;
    }

    private readonly List<CardView> cards = new List<CardView>();
    private readonly List<SlotView> slots = new List<SlotView>();

    private VisualElement grid;
    private VisualElement slotRow;
    private VisualElement pagePanel;
    private VisualElement coinPill;
    private VisualElement countPill;
    private Label coinsText;
    private Label selectedCountText;
    // 槽位信息条：悬停槽位时显示该铭文的名字与操作提示
    private Label slotInfoText;
    // UIDocument 在 GameObject 禁用后会拆掉可视树，重新激活时克隆一棵新树，
    // 所有缓存的元素引用都会失效；用 builtRoot + grid.panel 判断是否需要重建。
    private VisualElement builtRoot;

    private Coroutine alertRoutine;
    private IVisualElementScheduledItem shakeHandle;
    private VisualElement alertPill;
    private Label alertValue;

    public bool IsOpen => gameObject.activeSelf;

    // 本地没配就用全局铭文库，保证页面上看到的和角色结算用的是同一份数据
    private EngravingBlueprint[] Engravings
    {
        get { return allEngravings != null && allEngravings.Length > 0 ? allEngravings : EngravingManager.AllEngravings; }
    }

    // UIDocument 的 OnEnable 顺序与本组件没有保证，rootVisualElement 可能在本组件
    // 的 OnEnable 里还没准备好；下一帧再试一次，视觉上无感。
    private bool refreshScheduled;

    /// <summary>
    /// 组件启用时订阅铭文选择变化事件并安排一次延迟刷新。
    /// </summary>
    private void OnEnable()
    {
        EngravingManager.OnSelectedChanged += Refresh;
        ScheduleRefresh();
    }

    /// <summary>
    /// 组件停用时退订事件、取消排队刷新并停止闪烁与抖动。
    /// </summary>
    private void OnDisable()
    {
        EngravingManager.OnSelectedChanged -= Refresh;
        CancelInvoke(nameof(TryRefresh));
        refreshScheduled = false;
        // 关页后可视树会被拆掉，协程句柄不能留着跑在已销毁的元素上
        if (alertRoutine != null)
        {
            StopCoroutine(alertRoutine);
            alertRoutine = null;
        }
        shakeHandle?.Pause();
        shakeHandle = null;
        ClearFeedback();
    }

    /// <summary>
    /// 激活铭文强化页对象并安排一次界面刷新。
    /// </summary>
    public void Open()
    {
        gameObject.SetActive(true);
        ScheduleRefresh();
    }

    /// <summary>
    /// 隐藏铭文强化页对象，把界面交还给首页主界面。
    /// </summary>
    public void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 在当前帧末安排一次刷新，避免可视树未就绪时读取界面。
    /// </summary>
    private void ScheduleRefresh()
    {
        if (refreshScheduled) return;
        refreshScheduled = true;
        Invoke(nameof(TryRefresh), 0f);
    }

    /// <summary>
    /// 清除待刷新标记后执行一次界面刷新。
    /// </summary>
    private void TryRefresh()
    {
        refreshScheduled = false;
        Refresh();
    }

    /// <summary>
    /// 重建卡片与槽位、刷新卡片和顶部信息后播放入场动画。
    /// </summary>
    public void Refresh()
    {
        if (document == null)
            document = GetComponent<UIDocument>();
        if (document == null || document.rootVisualElement == null)
        {
            ScheduleRefresh();
            return;
        }

        var root = document.rootVisualElement;
        if (root.Q<VisualElement>("CardGrid") == null)
        {
            ScheduleRefresh();
            return;
        }

        Build(root);
        RefreshCards();
        RefreshHeader();
        RefreshSlots();

        // 树已建好才播入场动画。本页的 Refresh 还会被
        // EngravingManager.OnSelectedChanged 反复调用，UIAnimator 内部按树去重，
        // 不会在点卡片时把整页再淡入一遍。
        UIAnimator.PlayEnter(root);
    }

    /// <summary>
    /// 可视树换新时重新缓存元素引用、绑定返回按钮并生成卡片与槽位。
    /// </summary>
    private void Build(VisualElement root)
    {
        if (builtRoot == root && grid != null && grid.panel != null)
            return;

        // 旧树已失效：清空缓存引用和卡片/槽位，重新查询并绑定
        builtRoot = root;
        cards.Clear();
        slots.Clear();

        grid = root.Q<VisualElement>("CardGrid");
        slotRow = root.Q<VisualElement>("SlotRow");
        pagePanel = root.Q<VisualElement>("PagePanel");
        coinPill = root.Q<VisualElement>("CoinPill");
        countPill = root.Q<VisualElement>("CountPill");
        coinsText = root.Q<Label>("CoinsText");
        selectedCountText = root.Q<Label>("SelectedCountText");

        var backButton = root.Q<Button>("BackButton");
        if (backButton != null)
        {
            backButton.clicked -= OnBackClick; // 同一棵树上防重复绑定
            backButton.clicked += OnBackClick;
        }

        slotInfoText = root.Q<Label>("SlotInfoText");

        BuildCards();
        BuildSlots();
    }

    /// <summary>
    /// 在底栏显示指定槽位里铭文的名字与操作提示。
    /// </summary>
    private void ShowSlotInfo(string text)
    {
        if (slotInfoText == null)
            return;

        slotInfoText.text = text;
        slotInfoText.EnableInClassList("bottombar__hint--active", true);
    }

    /// <summary>
    /// 把底栏恢复成默认的槽位操作提示文案。
    /// </summary>
    private void ClearSlotInfo()
    {
        if (slotInfoText == null)
            return;

        slotInfoText.text = SlotInfoDefault;
        slotInfoText.EnableInClassList("bottombar__hint--active", false);
        slotInfoText.EnableInClassList("bottombar__hint--warn", false);
    }

    /// <summary>
    /// 底栏同时充当槽位提示与操作反馈：warn=true 时文案转红
    /// </summary>
    private void SetHint(string text, bool warn)
    {
        if (slotInfoText == null) return;

        slotInfoText.text = text;
        slotInfoText.EnableInClassList("bottombar__hint--active", !warn);
        slotInfoText.EnableInClassList("bottombar__hint--warn", warn);
    }

    // ------------------------------------------------------------------
    // 负反馈：金币不足 / 装备槽已满 → 对应胶囊红闪 + 面板轻微抖动
    // ------------------------------------------------------------------

    /// <summary>
    /// 对指定胶囊播放红闪并让整页抖动，作为操作被拒时的负反馈。
    /// </summary>
    private void TriggerFeedback(VisualElement pill, Label valueLabel)
    {
        if (alertRoutine != null)
        {
            StopCoroutine(alertRoutine);
            alertRoutine = null;
        }
        ClearFeedback(); // 换目标前先把两个胶囊都恢复，避免上一个胶囊留着红边

        alertPill = pill;
        alertValue = valueLabel;
        alertRoutine = StartCoroutine(AlertRoutine());

        shakeHandle?.Pause();
        shakeHandle = UIAnimator.Shake(pagePanel);
    }

    /// <summary>
    /// 在设定时长内让提示胶囊反复闪烁，结束后恢复原样式。
    /// </summary>
    private IEnumerator AlertRoutine()
    {
        float t = 0f;
        while (t < AlertDuration)
        {
            bool on = Mathf.Repeat(t, BlinkHalfPeriod * 2f) < BlinkHalfPeriod;
            ApplyAlertClass(on);
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        ClearFeedback();
        alertRoutine = null;
    }

    /// <summary>
    /// 清除两个胶囊的闪烁样式并复位面板位移，恢复正常外观。
    /// </summary>
    private void ClearFeedback()
    {
        if (coinPill != null)
        {
            coinPill.RemoveFromClassList("pill--alert");
            coinPill.RemoveFromClassList("pill--alert-dim");
        }
        if (countPill != null)
        {
            countPill.RemoveFromClassList("pill--alert");
            countPill.RemoveFromClassList("pill--alert-dim");
        }
        if (coinsText != null) coinsText.RemoveFromClassList("pill__v--alert");
        if (selectedCountText != null) selectedCountText.RemoveFromClassList("pill__v--alert");

        // 抖完清掉内联 translate，让 USS 的抬起量（投影）重新生效
        UIAnimator.ResetTranslate(pagePanel);
    }

    /// <summary>
    /// 根据闪烁相位给提示胶囊加上或去掉红闪样式。
    /// </summary>
    private void ApplyAlertClass(bool on)
    {
        if (alertPill != null)
        {
            alertPill.EnableInClassList("pill--alert", on);
            alertPill.EnableInClassList("pill--alert-dim", !on);
        }
        if (alertValue != null)
            alertValue.EnableInClassList("pill__v--alert", on);
    }

    /// <summary>
    /// 按铭文数据库逐个克隆卡片模板，绑定元素引用与操作按钮回调。
    /// </summary>
    private void BuildCards()
    {
        var source = Engravings;
        if (source == null || source.Length == 0)
        {
            Debug.LogError("[EngravingPageUITK] 铭文列表为空！请给 allEngravings 赋值，或用 Tools/Engraving/刷新铭文数据库 生成全局铭文库。");
            return;
        }

        if (cardTemplate == null)
        {
            Debug.LogError("[EngravingPageUITK] 卡片模板未赋值：请在 Inspector 里指定 EngravingCard.uxml！");
            return;
        }

        grid.Clear(); // 重建时清掉旧卡片，避免同一棵树上重复叠加

        foreach (var engraving in source)
        {
            if (engraving == null) continue;

            // 克隆 UXML 模板生成卡片，按 name 取元素绑定数据；
            // CardRoot 即 .lift--card（叠层伪造投影的底板）。
            var instance = cardTemplate.Instantiate();
            var card = instance.Q<VisualElement>("Card");
            if (card == null)
            {
                Debug.LogError("[EngravingPageUITK] 卡片模板缺少 name=\"Card\" 的元素！");
                return;
            }
            card.name = "Card_" + engraving.engravingId;

            var view = new CardView
            {
                engraving = engraving,
                root = card,
                icon = instance.Q<Image>("Icon"),
                name = instance.Q<Label>("NameLabel"),
                state = instance.Q<Label>("StateLabel"),
                effect = instance.Q<Label>("EffectLabel"),
                curse = instance.Q<Label>("CurseLabel"),
                action = instance.Q<Button>("ActionButton")
            };
            if (view.action != null)
                view.action.clicked += () => OnCardAction(view);

            grid.Add(instance.Q<VisualElement>("CardRoot"));
            cards.Add(view);
        }
    }

    /// <summary>
    /// 按可装备数量生成空槽位按钮，并挂上点击与悬停提示回调。
    /// </summary>
    private void BuildSlots()
    {
        for (int i = 0; i < EngravingManager.MaxSelectedSlots; i++)
        {
            var slot = new Button();
            slot.AddToClassList("slot");

            var icon = new Image { scaleMode = ScaleMode.ScaleToFit };
            icon.AddToClassList("slot__icon");
            icon.style.display = DisplayStyle.None;
            slot.Add(icon);

            int index = i;
            slot.clicked += () => OnSlotClick(index);

            var view = new SlotView { root = slot, icon = icon };
            slot.RegisterCallback<PointerEnterEvent>(evt => ShowSlotInfo(view.tooltipText));
            slot.RegisterCallback<PointerLeaveEvent>(evt => ClearSlotInfo());

            slotRow.Add(slot);
            slots.Add(view);
        }
    }

    /// <summary>
    /// 按解锁与装备状态更新每张铭文卡的文字、诅咒说明和按钮文案。
    /// </summary>
    private void RefreshCards()
    {
        var unlocked = EngravingManager.UnlockedEngravings;
        var selected = EngravingManager.SelectedEngravings;

        foreach (var card in cards)
        {
            var engraving = card.engraving;
            bool isUnlocked = unlocked.Contains(engraving.engravingId);
            bool isSelected = selected.Contains(engraving.engravingId);

            if (engraving.icon != null)
                card.icon.sprite = engraving.icon;

            // 卡片标题：优先用 displayName，为空时退回资产名，避免显示 UXML 占位字
            card.name.text = string.IsNullOrEmpty(engraving.displayName)
                ? engraving.name
                : engraving.displayName;

            card.state.text = isSelected ? "已装备" : (isUnlocked ? "未装备" : "未解锁");

            card.effect.text = engraving.description;
            card.effect.style.display = engraving.HasPositiveEffect() ? DisplayStyle.Flex : DisplayStyle.None;

            if (engraving.HasCurse())
            {
                card.curse.text = "诅咒 · " + engraving.curseDescription;
                card.curse.RemoveFromClassList("card__curse--none");
            }
            else
            {
                card.curse.text = "无诅咒";
                card.curse.AddToClassList("card__curse--none");
            }

            card.root.EnableInClassList("card--equipped", isSelected);
            card.action.EnableInClassList("btn--primary", isSelected);
            card.action.EnableInClassList("btn--ghost", !isSelected);

            if (!isUnlocked)
                card.action.text = engraving.unlockCost + "金币解锁";
            else if (isSelected)
                card.action.text = "已装备 · 卸下";
            else
                card.action.text = "装备";
        }
    }

    /// <summary>
    /// 显示当前金币数以及已装备铭文占全部槽位的数量。
    /// </summary>
    private void RefreshHeader()
    {
        if (coinsText != null)
            coinsText.text = DataStoreLocator.Store.GetCoins().ToString();

        if (selectedCountText != null)
            selectedCountText.text = EngravingManager.SelectedEngravings.Count + " / " + EngravingManager.MaxSelectedSlots;
    }

    /// <summary>
    /// 按已装备铭文填充各槽位的图标或首字，禁用空槽位并恢复提示。
    /// </summary>
    private void RefreshSlots()
    {
        var selected = EngravingManager.SelectedEngravings;
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            bool filled = i < selected.Count;
            var engraving = filled ? EngravingManager.GetEngraving(selected[i], Engravings) : null;
            bool hasIcon = engraving != null && engraving.icon != null;

            // 已装备的槽位显示铭文图标；没有图标时退回显示名字首字，空槽位显示＋
            slot.icon.sprite = engraving != null ? engraving.icon : null;
            slot.icon.style.display = hasIcon ? DisplayStyle.Flex : DisplayStyle.None;

            if (engraving != null && !hasIcon)
                slot.root.text = string.IsNullOrEmpty(engraving.displayName) ? "·" : engraving.displayName.Substring(0, 1);
            else
                slot.root.text = hasIcon ? string.Empty : "＋";

            slot.tooltipText = engraving != null
                ? engraving.displayName + " · 点击卸下"
                : "空槽位";

            slot.root.EnableInClassList("slot--filled", filled);
            slot.root.SetEnabled(filled);
        }

        ClearSlotInfo();
    }

    /// <summary>
    /// 音效缺失（如场景里没有 AudioManager）时不能让交互失败，否则会卡在页面上
    /// </summary>
    private static void PlayClickSafely()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayClick();
    }

    /// <summary>
    /// 按解锁与装备状态执行解锁扣费、装备或拦截槽位已满的情况。
    /// </summary>
    private void OnCardAction(CardView card)
    {
        PlayClickSafely();
        var engraving = card.engraving;
        if (engraving == null) return;

        if (!EngravingManager.IsUnlocked(engraving.engravingId))
        {
            // 解锁结果要等服务端回包，成功/失败都在这里收口给反馈
            EngravingManager.Unlock(engraving, success =>
            {
                if (!success)
                {
                    Refresh();
                    int missing = engraving.unlockCost - DataStoreLocator.Store.GetCoins();
                    SetHint(missing > 0 ? "金币不足，还差 " + missing + " 金币" : "解锁失败", true);
                    TriggerFeedback(coinPill, coinsText);
                    return;
                }

                CoinDisplayManager.UpdateAll();
                Refresh();
                SetHint("已解锁「" + engraving.displayName + "」，点击装备", false);
            });
            return;
        }

        // 槽位已满时 EngravingManager.Select 会静默失败，这里拦下来给反馈
        bool isSelected = EngravingManager.SelectedEngravings.Contains(engraving.engravingId);
        if (!isSelected && EngravingManager.SelectedEngravings.Count >= EngravingManager.MaxSelectedSlots)
        {
            Refresh();
            SetHint("最多装备 " + EngravingManager.MaxSelectedSlots + " 个铭文，请先卸下一个", true);
            TriggerFeedback(countPill, selectedCountText);
            return;
        }

        EngravingManager.Select(engraving);
    }

    /// <summary>
    /// 点击已占用的槽位时卸下该位置上的铭文。
    /// </summary>
    private void OnSlotClick(int index)
    {
        var selected = EngravingManager.SelectedEngravings;
        if (index < 0 || index >= selected.Count)
            return;

        var engraving = EngravingManager.GetEngraving(selected[index], Engravings);
        if (engraving == null)
            return;

        PlayClickSafely();
        EngravingManager.Select(engraving);
    }

    /// <summary>
    /// 播放点击音效后关闭铭文强化页回到首页。
    /// </summary>
    private void OnBackClick()
    {
        PlayClickSafely();
        Close();
    }
}
