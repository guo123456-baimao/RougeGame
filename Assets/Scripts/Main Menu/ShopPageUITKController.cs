using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
/// <summary>
/// UI Toolkit 版商店页。商品列表直接挂在组件上（shopItems）。
/// 单局规则由 ShopSession 统一裁决：同一件商品一局内不能重复购买，每局最多 3 件，
/// 一局结束（Character.ClearSessionData）后清空，购买过的卡片在本局内变灰。
/// </summary>
public class ShopPageUITKController : MonoBehaviour
{
    [SerializeField] private UIDocument document;
    [SerializeField] private ShopItemBlueprint[] shopItems;
    // 商品卡结构来自 UXML 模板（Assets/UI/ShopCard.uxml），
    // 卡片样式在 UI Builder 里改模板即可，无需改代码。
    [SerializeField] private VisualTreeAsset cardTemplate;

#if UNITY_EDITOR
    /// <summary>
    /// 模板引用留空时自动补上，省去每个场景手动拖一次
    /// </summary>
    private void OnValidate()
    {
        if (cardTemplate == null)
            cardTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/ShopCard.uxml");
    }
#endif

    private const string HintDefault = "点击「购买」后立即生效";
    private const float AlertDuration = 1.2f;
    private const float BlinkHalfPeriod = 0.08f;
    private const float ShakeDuration = 0.28f;
    private const float ShakeMaxOffset = 6f;
    // 金币滚动时长：比成绩结算慢一点、比瞬变快一点
    private const long CoinRollDurationMs = 500;

    private class CardView
    {
        public ShopItemBlueprint item;
        public VisualElement root;
        public Image icon;
        public Label name;
        public Label type;
        public Label desc;
        public Label price;
        public Button action;
    }

    private readonly List<CardView> cards = new List<CardView>();

    private VisualElement grid;
    private VisualElement pagePanel;
    private VisualElement coinPill;
    private VisualElement countPill;
    private Label coinsText;
    private Label purchasedCountText;
    private Label statsText;
    private Label hintText;
    // UIDocument 在 GameObject 禁用后会拆掉可视树，重新激活时克隆一棵新树，
    // 所有缓存的元素引用都会失效；用 builtRoot + grid.panel 判断是否需要重建。
    private VisualElement builtRoot;

    private Coroutine alertRoutine;
    private Coroutine shakeRoutine;
    private VisualElement alertPill;
    private Label alertValue;

    // 金币「画出来的值」与滚动补间句柄。displayedCoins < 0 表示还没落位（首次/重建树后）
    private int displayedCoins = -1;
    private IVisualElementScheduledItem coinTween;

    public bool IsOpen => gameObject.activeSelf;

    // UIDocument 的 OnEnable 顺序与本组件没有保证，rootVisualElement 可能在本组件
    // 的 OnEnable 里还没准备好；下一帧再试一次，视觉上无感。
    private bool refreshScheduled;

    /// <summary>
    /// 组件启用时安排一次延迟刷新，等待可视树准备就绪。
    /// </summary>
    private void OnEnable()
    {
        ScheduleRefresh();
    }

    /// <summary>
    /// 组件停用时取消排队刷新，并清掉协程标记、金币滚动与红闪残留样式。
    /// </summary>
    private void OnDisable()
    {
        CancelInvoke(nameof(TryRefresh));
        refreshScheduled = false;
        // 协程会随 GameObject 失活一起停，这里同步清掉标记与残留样式
        alertRoutine = null;
        shakeRoutine = null;
        // 关页后可视树会被拆掉，滚动句柄不能留着跑在已销毁的元素上
        coinTween?.Pause();
        coinTween = null;
        displayedCoins = -1;
        ClearLimitFeedback();
    }

    /// <summary>
    /// 组件销毁时清空红闪与抖动协程引用，避免继续操作已销毁界面。
    /// </summary>
    private void OnDestroy()
    {
        alertRoutine = null;
        shakeRoutine = null;
    }

    /// <summary>
    /// 激活商城页对象并安排一次界面刷新。
    /// </summary>
    public void Open()
    {
        gameObject.SetActive(true);
        ScheduleRefresh();
    }

    /// <summary>
    /// 隐藏商城页并统一刷新各处金币显示。
    /// </summary>
    public void Close()
    {
        gameObject.SetActive(false);
        CoinDisplayManager.UpdateAll();
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
    /// 重建商品卡片并刷新金币、已购数量与属性汇总，最后播放入场动画。
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
        if (root.Q<VisualElement>("ItemGrid") == null)
        {
            ScheduleRefresh();
            return;
        }

        Build(root);
        ClearLimitFeedback();
        RefreshCards();
        RefreshHeader();
        RefreshStats();
        SetHint(HintDefault, false);

        // 树已经建好、数据也刷完了，这时候才播入场动画。
        // 页面靠 SetActive 切换，每次重新激活都会走 OnEnable → Refresh，动画随之重播。
        UIAnimator.PlayEnter(root);
    }

    /// <summary>
    /// 可视树换新时重新缓存元素引用、绑定返回按钮并生成商品卡，同时重置金币显示。
    /// </summary>
    private void Build(VisualElement root)
    {
        if (builtRoot == root && grid != null && grid.panel != null)
            return;

        // 旧树已失效：清空缓存引用和卡片，重新查询并绑定
        builtRoot = root;
        cards.Clear();

        // 这是一棵新树：金币的显示值作废，下次 RefreshHeader 直接落位，
        // 既避免空 Label，也避免每次回到商店页数字都从旧值/0 滚一遍
        coinTween?.Pause();
        coinTween = null;
        displayedCoins = -1;

        grid = root.Q<VisualElement>("ItemGrid");
        pagePanel = root.Q<VisualElement>("PagePanel");
        coinPill = root.Q<VisualElement>("CoinPill");
        countPill = root.Q<VisualElement>("CountPill");
        coinsText = root.Q<Label>("CoinsText");
        purchasedCountText = root.Q<Label>("PurchasedCountText");
        statsText = root.Q<Label>("StatsText");
        hintText = root.Q<Label>("HintText");

        var backButton = root.Q<Button>("BackButton");
        if (backButton != null)
        {
            backButton.clicked -= OnCloseClick; // 同一棵树上防重复绑定
            backButton.clicked += OnCloseClick;
        }

        var closeButton = root.Q<Button>("CloseButton");
        if (closeButton != null)
        {
            closeButton.clicked -= OnCloseClick;
            closeButton.clicked += OnCloseClick;
        }

        BuildCards();
    }

    /// <summary>
    /// 返回组件上配置的商品蓝图数组作为商店的数据源。
    /// </summary>
    private ShopItemBlueprint[] GetItems()
    {
        return shopItems;
    }

    /// <summary>
    /// 按商品蓝图逐个克隆卡片模板，绑定元素引用与购买按钮回调。
    /// </summary>
    private void BuildCards()
    {
        var items = GetItems();
        if (items == null)
        {
            Debug.LogError("[ShopPageUITK] 没有商品数据：shopItems 数组为空！");
            return;
        }

        if (cardTemplate == null)
        {
            Debug.LogError("[ShopPageUITK] 卡片模板未赋值：请在 Inspector 里指定 ShopCard.uxml！");
            return;
        }

        grid.Clear(); // 重建时清掉旧卡片，避免同一棵树上重复叠加

        foreach (var item in items)
        {
            if (item == null) continue;

            // 克隆 UXML 模板生成卡片，按 name 取元素绑定数据；
            // CardRoot 即 .lift--card（叠层伪造投影的底板）。
            var instance = cardTemplate.Instantiate();
            var card = instance.Q<VisualElement>("Card");
            if (card == null)
            {
                Debug.LogError("[ShopPageUITK] 卡片模板缺少 name=\"Card\" 的元素！");
                return;
            }
            card.name = "Card_" + item.name;

            var view = new CardView
            {
                item = item,
                root = card,
                icon = instance.Q<Image>("Icon"),
                name = instance.Q<Label>("NameLabel"),
                type = instance.Q<Label>("TypeLabel"),
                desc = instance.Q<Label>("DescLabel"),
                price = instance.Q<Label>("PriceLabel"),
                action = instance.Q<Button>("ActionButton")
            };
            if (view.action != null)
                view.action.clicked += () => OnBuyClick(view);

            grid.Add(instance.Q<VisualElement>("CardRoot"));
            cards.Add(view);
        }
    }

    /// <summary>
    /// 按本局是否已购买与金币是否足够更新每张商品卡的文字与按钮状态。
    /// </summary>
    private void RefreshCards()
    {
        int coins = DataStoreLocator.Store.GetCoins();

        foreach (var card in cards)
        {
            var item = card.item;
            // 本局已购买 → 变灰，禁止再次购买（避免属性叠加）
            bool locked = ShopSession.IsPurchased(item);
            int cost = item.GetCost();
            bool affordable = coins >= cost;

            card.icon.sprite = item.icon;
            card.name.text = item.GetDisplayName();
            card.type.text = GetTypeName(item.itemType);

            var description = item.GetDescription();
            card.desc.text = string.IsNullOrEmpty(description) ? "暂无描述" : description;
            card.desc.EnableInClassList("card__desc--none", string.IsNullOrEmpty(description));

            card.price.text = "价格 " + cost;
            card.price.EnableInClassList("card__price--purchased", locked);

            card.root.EnableInClassList("card--locked", locked);
            card.action.EnableInClassList("btn--primary", !locked && affordable);
            card.action.EnableInClassList("btn--ghost", locked || !affordable);
            card.action.SetEnabled(!locked);
            card.action.text = locked ? "已购买" : (affordable ? "购买" : "金币不足");
        }
    }

    /// <summary>
    /// 把金币数字滚动到最新值并显示本局已购件数与上限。
    /// </summary>
    private void RefreshHeader()
    {
        RollCoins(DataStoreLocator.Store.GetCoins());

        if (purchasedCountText != null)
            purchasedCountText.text = ShopSession.PurchasedCount + " / " + ShopSession.MaxPurchasesPerRound;
    }

    /// <summary>
    /// 金币数字滚动到目标值。displayedCoins &lt; 0（首次进入 / 可视树重建）时直接落位，
    /// 不从 0 滚上来；购买导致数值变化时走补间。
    /// </summary>
    private void RollCoins(int target)
    {
        if (coinsText == null) return;

        if (displayedCoins < 0)
        {
            displayedCoins = target;
            ApplyCoinText();
            return;
        }

        if (displayedCoins == target) return;

        coinTween?.Pause();
        coinTween = UIAnimator.NumberTo(coinsText, displayedCoins, target, CoinRollDurationMs,
            v => { displayedCoins = v; ApplyCoinText(); });
    }

    /// <summary>
    /// 把当前已绘制的金币数值写到顶部数字标签上。
    /// </summary>
    private void ApplyCoinText()
    {
        if (coinsText != null) coinsText.text = displayedCoins.ToString();
    }

    /// <summary>
    /// 底栏显示本局已购商品叠加出来的属性
    /// </summary>
    private void RefreshStats()
    {
        if (statsText == null) return;

        string bonus = ShopSession.GetBonusText(GetItems());
        statsText.text = string.IsNullOrEmpty(bonus) ? "暂无" : bonus;
        statsText.EnableInClassList("bottombar__stats--none", string.IsNullOrEmpty(bonus));
    }

    /// <summary>
    /// 把商品类型枚举转换成界面上显示的中文分类名。
    /// </summary>
    private static string GetTypeName(ShopItemBlueprint.ShopItemType type)
    {
        switch (type)
        {
            case ShopItemBlueprint.ShopItemType.StatBoost: return "属性强化";
            case ShopItemBlueprint.ShopItemType.Consumable: return "消耗品";
            default: return "商品";
        }
    }

    /// <summary>
    /// 更新底栏提示文字，并按是否警告切换提示的颜色样式。
    /// </summary>
    private void SetHint(string text, bool warn)
    {
        if (hintText == null) return;

        hintText.text = text;
        hintText.EnableInClassList("bottombar__hint--active", !warn);
        hintText.EnableInClassList("bottombar__hint--warn", warn);
    }

    /// <summary>
    /// 音效缺失（如场景里没有 AudioManager）时不能让交互失败，否则会卡在页面上
    /// </summary>
    private static void PlayClickSafely()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayClick();
    }

    // 联网购买要等服务器回包，期间挡住连点，避免重复扣费
    private bool purchasePending;

    /// <summary>
    /// 校验重复购买、件数上限与金币后发起扣费，成功才记入本局加成。
    /// </summary>
    private void OnBuyClick(CardView card)
    {
        PlayClickSafely();

        var item = card.item;
        if (item == null || purchasePending) return;

        switch (ShopSession.CheckPurchase(item))
        {
            case ShopDenyReason.AlreadyPurchased:
                SetHint("「" + item.GetDisplayName() + "」本局已购买，不能重复叠加", true);
                return;

            case ShopDenyReason.LimitReached:
                SetHint("本局最多购买 " + ShopSession.MaxPurchasesPerRound + " 件商品，已达上限", true);
                TriggerLimitFeedback();
                return;

            case ShopDenyReason.NotEnoughCoins:
                int missing = item.GetCost() - DataStoreLocator.Store.GetCoins();
                SetHint("金币不足，还差 " + missing + " 金币", true);
                TriggerFeedback(coinPill, coinsText);
                return;
        }

        // 扣费由服务端（或本地存储）事务执行，回包后才把本局加成真正记上去
        purchasePending = true;
        DataStoreLocator.Store.SpendCoins(item.CreateSpendOrder(), outcome =>
        {
            purchasePending = false;

            if (!outcome.Success)
            {
                SetHint(outcome.Message, true);
                TriggerFeedback(coinPill, coinsText);
                RefreshHeader();
                return;
            }

            item.Purchase();
            ShopSession.Record(item);
            SetHint("已购买「" + item.GetDisplayName() + "」，加成已计入本局", false);

            CoinDisplayManager.UpdateAll();
            RefreshCards();
            RefreshHeader();
            RefreshStats();
        });
    }

    /// <summary>
    /// 达到每局上限后再点购买：计数胶囊红闪 + 面板轻微震动
    /// </summary>
    private void TriggerLimitFeedback()
    {
        TriggerFeedback(countPill, purchasedCountText);
    }

    /// <summary>
    /// 通用负反馈：指定胶囊红闪 + 面板轻微震动（金币不足 / 达到上限共用）
    /// </summary>
    private void TriggerFeedback(VisualElement pill, Label valueLabel)
    {
        ClearLimitFeedback(); // 换目标前先把两个胶囊都恢复，避免上一个胶囊留着红边

        alertPill = pill;
        alertValue = valueLabel;

        if (alertRoutine == null)
            alertRoutine = StartCoroutine(AlertRoutine());

        if (shakeRoutine == null)
            shakeRoutine = StartCoroutine(ShakeRoutine());
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

        ClearLimitFeedback();
        alertRoutine = null;
    }

    /// <summary>
    /// 恢复计数胶囊与面板的正常状态（闪烁结束 / 关页 / 刷新时调用）
    /// </summary>
    private void ClearLimitFeedback()
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
        if (purchasedCountText != null)
            purchasedCountText.RemoveFromClassList("pill__v--alert");
        if (pagePanel != null && pagePanel.panel != null)
            // 清掉内联 translate，交还给 USS 的 .lift > *（抬起 4px 露投影）。
            // 不能复位成 Translate(0,0)：内联样式优先级最高，会把面板压回投影层里，投影就没了。
            pagePanel.style.translate = new StyleTranslate(StyleKeyword.Null);
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
    /// 在设定时长内让整页随机抖动且幅度递减，结束后复位位置。
    /// </summary>
    private IEnumerator ShakeRoutine()
    {
        if (pagePanel == null) yield break;

        float t = 0f;
        while (t < ShakeDuration)
        {
            t += Time.unscaledDeltaTime;
            float falloff = 1f - Mathf.Clamp01(t / ShakeDuration);
            float x = UnityEngine.Random.Range(-ShakeMaxOffset, ShakeMaxOffset) * falloff;
            float y = UnityEngine.Random.Range(-ShakeMaxOffset, ShakeMaxOffset) * falloff;
            pagePanel.style.translate = new Translate(x, y);
            yield return null;
        }

        // 同上：抖完清掉内联 translate，让 USS 的抬起量（投影）重新生效
        pagePanel.style.translate = new StyleTranslate(StyleKeyword.Null);
        shakeRoutine = null;
    }

    /// <summary>
    /// 播放点击音效后关闭商城页。
    /// </summary>
    private void OnCloseClick()
    {
        PlayClickSafely();
        Close();
    }
}
