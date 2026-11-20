using UnityEngine;
using UnityEngine.UIElements;
/// <summary>
/// UI Toolkit 版首页。三个入口的落点：
/// 商城 → ShopPageUITKController.Open，开始 → MainMenu.OpenCharacterPage，强化 → MainMenu.OpenEngravingPage。
/// </summary>
public class HomePageUITKController : MonoBehaviour
{
    [SerializeField] private UIDocument document;
    [SerializeField] private MainMenu mainMenu;
    [SerializeField] private ShopPageUITKController shopPage;
    [SerializeField] private LoginPageUITKController loginPage;

    private VisualElement accountArea;
    private Button accountButton;
    private Label accountNameLabel;
    private Button logoutButton;

    // UIDocument 的 OnEnable 顺序与本组件没有保证，rootVisualElement 可能还没准备好；
    // 下一帧再试一次，视觉上无感。
    private bool bindScheduled;

    /// <summary>
    /// 组件启用时安排一次绑定并订阅会话变更事件，用于驱动账户区刷新。
    /// </summary>
    private void OnEnable()
    {
        ScheduleBind();

        // 登录 / 退出都会换掉数据源，交给事件统一驱动界面刷新，
        // 省得每个页面各自去记「现在登录了没」。
        ServerSession.SessionChanged -= OnSessionChanged;
        ServerSession.SessionChanged += OnSessionChanged;
    }

    /// <summary>
    /// 组件停用时取消排队中的绑定任务并退订会话变更事件。
    /// </summary>
    private void OnDisable()
    {
        CancelInvoke(nameof(TryBind));
        bindScheduled = false;
        ServerSession.SessionChanged -= OnSessionChanged;
    }

    /// <summary>
    /// 在当前帧末安排一次绑定，同一时刻只保留一个待执行任务。
    /// </summary>
    private void ScheduleBind()
    {
        if (bindScheduled) return;
        bindScheduled = true;
        Invoke(nameof(TryBind), 0f);
    }

    /// <summary>
    /// 清除待绑定标记后立即执行一次界面元素绑定。
    /// </summary>
    private void TryBind()
    {
        bindScheduled = false;
        Bind();
    }

    /// <summary>
    /// 等可视树就绪后绑定各入口点击事件与账户区元素，并播放入场动画。
    /// </summary>
    public void Bind()
    {
        if (document == null)
            document = GetComponent<UIDocument>();
        if (document == null || document.rootVisualElement == null)
        {
            ScheduleBind();
            return;
        }

        var root = document.rootVisualElement;
        if (root.Q<Button>("ShopButton") == null)
        {
            ScheduleBind();
            return;
        }

        // 方块和文字按钮都算入口；-= 后再 += 保证重新激活时不重复触发
        BindClick(root, "ShopTile", OnShopClick);
        BindClick(root, "ShopButton", OnShopClick);
        BindClick(root, "StartTile", OnStartClick);
        BindClick(root, "StartButton", OnStartClick);
        BindClick(root, "UpgradeTile", OnUpgradeClick);
        BindClick(root, "UpgradeButton", OnUpgradeClick);
        BindClick(root, "AccountButton", OnAccountClick);
        BindClick(root, "LogoutButton", OnLogoutClick);

        accountArea = root.Q<VisualElement>("AccountArea");
        accountButton = root.Q<Button>("AccountButton");
        accountNameLabel = root.Q<Label>("AccountNameLabel");
        logoutButton = root.Q<Button>("LogoutButton");

        // 会话恢复是异步的，绑定时未必已经拿到账户名，这里按当前状态刷一次，
        // 之后由 SessionChanged 事件兜住。
        RefreshAccountArea();

        // 绑定完成后播入场动画：标题栏 → 商城 → 开始 → 强化 依次进场
        UIAnimator.PlayEnter(root);
    }

    /// <summary>
    /// 找到指定名称的按钮并重新挂接点击回调，找不到时输出警告。
    /// </summary>
    private static void BindClick(VisualElement root, string elementName, System.Action handler)
    {
        var button = root.Q<Button>(elementName);
        if (button == null)
        {
            Debug.LogWarning("[HomePageUITK] 找不到元素：" + elementName);
            return;
        }

        button.clicked -= handler;
        button.clicked += handler;
    }

    /// <summary>
    /// 音效缺失（独立预览页面时没有 AudioManager）不能让点击失败
    /// </summary>
    private static void PlayClickSafely()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayClick();
    }

    /// <summary>
    /// 打开商城页，未挂载商城控制器时输出警告提示。
    /// </summary>
    private void OnShopClick()
    {
        PlayClickSafely();

        if (shopPage != null)
            shopPage.Open();
        else
            Debug.LogWarning("[HomePageUITK] 没有挂 ShopPageUITKController，「商城」入口无响应。");
    }

    /// <summary>
    /// 点开始后通过主菜单打开角色选择页，开始一局新游戏。
    /// </summary>
    private void OnStartClick()
    {
        PlayClickSafely();

        if (mainMenu != null)
            mainMenu.OpenCharacterPage();
        else
            Debug.LogWarning("[HomePageUITK] 没有挂 MainMenu，「开始」入口无响应。");
    }

    /// <summary>
    /// 通过主菜单打开铭文强化页，点击音由主菜单统一播放。
    /// </summary>
    private void OnUpgradeClick()
    {
        // MainMenu.OpenEngravingPage 内部已经播了点击音，这里不再重复
        if (mainMenu != null)
            mainMenu.OpenEngravingPage();
        else
            Debug.LogWarning("[HomePageUITK] 没有挂 MainMenu，「强化」入口无响应。");
    }

    /// <summary>
    /// 打开登录页，让玩家登录或注册云存档账号。
    /// </summary>
    private void OnAccountClick()
    {
        PlayClickSafely();

        if (loginPage != null)
            loginPage.Open();
        else
            Debug.LogWarning("[HomePageUITK] 没有挂 LoginPageUITK，「登录」入口无响应。");
    }

    /// <summary>
    /// 交给会话服务执行退出登录，由其广播事件触发界面刷新。
    /// </summary>
    private void OnLogoutClick()
    {
        PlayClickSafely();

        // 退出由 ServerSession 统一处理：清 token + 切回本地存储，
        // 然后它会广播 SessionChanged，本页的账户区和其它页面的金币一起刷新。
        ServerSession.Logout();
    }

    /// <summary>
    /// 登录状态变化后刷新账户区显示并重读当前身份的金币数。
    /// </summary>
    private void OnSessionChanged()
    {
        RefreshAccountArea();
        // 数据源换人了（云端 ↔ 本地），金币要立刻按新身份重读，否则还停留在上一个账号的数字上
        CoinDisplayManager.UpdateAll();
    }

    /// <summary>账户区两态互斥：未登录 [登录]；已登录 账户名 + [退出]。</summary>
    private void RefreshAccountArea()
    {
        bool loggedIn = ServerSession.IsLoggedIn;

        if (accountButton != null)
            accountButton.style.display = loggedIn ? DisplayStyle.None : DisplayStyle.Flex;

        if (accountNameLabel != null)
        {
            accountNameLabel.text = loggedIn ? ServerSession.Username : string.Empty;
            accountNameLabel.style.display = loggedIn ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (logoutButton != null)
            logoutButton.style.display = loggedIn ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
