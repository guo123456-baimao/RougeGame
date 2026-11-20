using System;
using UnityEngine;
using UnityEngine.UIElements;
/// <summary>
/// 登录页（UI Toolkit 版）。
///
/// 这里是小白玩家的入口：填账号密码 → 点登录 → 成功后 DataStoreLocator 整体切到
/// ApiDataStore，金币 / 角色 / 铭文都改成读写服务器，然后自动关掉本页回到主界面。
/// 失败则原地提示，存档不丢。
///
/// 注册已拆到 RegisterPage.uxml / RegisterPageUITKController，这里只留跳转入口；
/// 退出登录移到首页标题栏的账户区（账户名旁边），登录后随时可点。
/// 不登录也能玩，此时走的是本地存档，所以服务器挂了不影响单机体验。
/// </summary>
public class LoginPageUITKController : MonoBehaviour
{
    [SerializeField] private UIDocument document;
    [SerializeField] private RegisterPageUITKController registerPage;

    private VisualElement panel;
    private TextField usernameField;
    private TextField passwordField;
    private Label stateLabel;
    private Label messageLabel;
    private Button loginButton;
    private Button registerButton;

    private bool bindScheduled;
    private bool busy;

    public bool IsOpen => gameObject.activeSelf;

    private void OnEnable() => ScheduleBind();

    /// <summary>
    /// 组件停用时取消排队中的绑定任务并清除绑定标记。
    /// </summary>
    private void OnDisable()
    {
        CancelInvoke(nameof(TryBind));
        bindScheduled = false;
    }

    /// <summary>
    /// 激活登录页对象并显示在首页之上，供玩家输入账号密码。
    /// </summary>
    public void Open()
    {
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 隐藏登录页对象，把界面交还回首页继续操作。
    /// </summary>
    public void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// UIDocument 的 OnEnable 顺序与本组件没有保证，rootVisualElement 可能还没准备好；
    /// 下一帧再试一次，视觉上无感。
    /// </summary>
    private void ScheduleBind()
    {
        if (bindScheduled) return;
        bindScheduled = true;
        Invoke(nameof(TryBind), 0f);
    }

    /// <summary>
    /// 清除标记后立即执行一次界面元素绑定。
    /// </summary>
    private void TryBind()
    {
        bindScheduled = false;
        Bind();
    }

    /// <summary>
    /// 取到输入框与按钮后限制长度、挂接回调并刷新状态与初始提示。
    /// </summary>
    private void Bind()
    {
        if (document == null)
            document = GetComponent<UIDocument>();
        if (document == null || document.rootVisualElement == null)
        {
            ScheduleBind();
            return;
        }

        var root = document.rootVisualElement;

        panel = root.Q<VisualElement>("LoginPanel");
        usernameField = root.Q<TextField>("UsernameField");
        passwordField = root.Q<TextField>("PasswordField");
        stateLabel = root.Q<Label>("StateLabel");
        messageLabel = root.Q<Label>("MessageLabel");
        loginButton = root.Q<Button>("LoginButton");
        registerButton = root.Q<Button>("RegisterButton");

        // UXML 还没建好（AssetDatabase 首帧延迟），下一帧再来
        if (usernameField == null || loginButton == null)
        {
            ScheduleBind();
            return;
        }

        // 密码框遮挡由代码设置，避免依赖 UXML 属性名在不同 Unity 版本上的差异
        passwordField.isPasswordField = true;
        passwordField.maxLength = 64;
        usernameField.maxLength = 24;

        BindClick(loginButton, OnLoginClick);
        BindClick(registerButton, OnRegisterClick);
        BindClick(root.Q<Button>("CloseButton"), OnCloseClick);

        RefreshState();
        SetMessage("未登录也可以正常游戏，存档保存在本机。", MessageKind.Normal);

        UIAnimator.PlayEnter(root);
    }

    /// <summary>
    /// 把点击回调挂到按钮上，先移除再添加以免重复触发。
    /// </summary>
    private static void BindClick(Button button, Action handler)
    {
        if (button == null) return;

        button.clicked -= handler;
        button.clicked += handler;
    }

    /// <summary>
    /// 音效缺失（独立预览面板时没有 AudioManager）不能让点击失败
    /// </summary>
    private static void PlayClickSafely()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayClick();
    }

    /// <summary>
    /// 先校验输入是否合法，通过后播放音效并发起登录请求。
    /// </summary>
    private void OnLoginClick()
    {
        if (!TryCollectCredentials(out string username, out string password, out string error)) return;

        PlayClickSafely();
        Submit(username, password, ServerSession.Login);
    }

    /// <summary>
    /// 跳到注册页。注册已拆成独立页面，这里只做跳转并带上已填的账号，
    /// 不再校验账号密码 —— 校验是注册页自己的事。
    /// </summary>
    private void OnRegisterClick()
    {
        PlayClickSafely();

        if (registerPage == null)
        {
            Debug.LogWarning("[LoginPageUITK] 没有挂 RegisterPageUITK，「注册新账号」入口无响应。");
            return;
        }

        registerPage.SetUsername(usernameField != null ? usernameField.value : string.Empty);
        registerPage.Open();
        Close();
    }

    /// <summary>
    /// 播放点击音效后关闭登录页回到首页。
    /// </summary>
    private void OnCloseClick()
    {
        PlayClickSafely();
        Close();
    }

    /// <summary>
    /// 读取并校验账号密码，不合格时在页面上提示原因并返回失败。
    /// </summary>
    private bool TryCollectCredentials(out string username, out string password, out string error)
    {
        username = string.Empty;
        password = string.Empty;
        error = null;

        if (busy) return false;

        if (usernameField == null || passwordField == null) return false;

        username = usernameField.value?.Trim() ?? string.Empty;
        password = passwordField.value ?? string.Empty;

        if (username.Length == 0)
        {
            SetMessage("请输入账号。", MessageKind.Error);
            return false;
        }

        if (password.Length == 0)
        {
            SetMessage("请输入密码。", MessageKind.Error);
            return false;
        }

        // 与服务端一致：密码至少 6 位，提前挡掉一次必然失败的请求
        if (password.Length < 6)
        {
            SetMessage("密码至少 6 位。", MessageKind.Error);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 禁用按钮并发起登录，成功后清空密码、刷新状态与金币并关闭页面。
    /// </summary>
    private void Submit(string username, string password, Action<string, string, Action<bool, string>> auth)
    {
        busy = true;
        SetInteractive(false);
        SetMessage("正在连接服务器…", MessageKind.Normal);

        auth(username, password, (ok, message) =>
        {
            busy = false;
            SetInteractive(true);

            if (!ok)
            {
                SetMessage(message, MessageKind.Error);
                return;
            }

            // 登录成功：云存档已经接管 DataStoreLocator，界面显示的数据源随之切换。
            // 随即关闭本页回到主界面，账户名由首页标题栏的账户区显示。
            passwordField.value = string.Empty;
            usernameField.value = username;
            RefreshState();
            SetMessage(message, MessageKind.Success);
            CoinDisplayManager.UpdateAll();
            Close();
        });
    }

    /// <summary>预先填好账号（比如从注册页带回来）。绑定完成前调用也没问题。</summary>
    public void SetUsername(string username)
    {
        if (usernameField != null)
            usernameField.value = username ?? string.Empty;
    }

    /// <summary>
    /// 在请求进行中禁用或恢复登录与注册按钮的点击。
    /// </summary>
    private void SetInteractive(bool interactive)
    {
        if (loginButton != null) loginButton.SetEnabled(interactive);
        if (registerButton != null) registerButton.SetEnabled(interactive);
    }

    /// <summary>
    /// 根据当前是否登录显示云存档或本地存档的状态说明。
    /// </summary>
    private void RefreshState()
    {
        if (stateLabel == null) return;

        stateLabel.text = ServerSession.IsLoggedIn
            ? "当前：云存档（" + ServerSession.Username + "）— 金币与解锁都会同步到服务器"
            : "当前：本地存档（未登录也能玩）";
    }

    private enum MessageKind { Normal, Success, Error }

    /// <summary>
    /// 更新提示文字，并按成功或失败切换文字的颜色样式。
    /// </summary>
    private void SetMessage(string text, MessageKind kind)
    {
        if (messageLabel == null) return;

        messageLabel.text = text;
        messageLabel.RemoveFromClassList("cloud__msg--ok");
        messageLabel.RemoveFromClassList("cloud__msg--err");

        if (kind == MessageKind.Success)
            messageLabel.AddToClassList("cloud__msg--ok");
        else if (kind == MessageKind.Error)
            messageLabel.AddToClassList("cloud__msg--err");
    }
}
