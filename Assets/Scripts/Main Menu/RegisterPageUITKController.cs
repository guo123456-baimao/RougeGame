using System;
using UnityEngine;
using UnityEngine.UIElements;
/// <summary>
/// 注册页（UI Toolkit 版）。
///
/// 从登录页拆出来的独立页面：填账号 + 密码 + 确认密码 → 注册 →
/// 服务端返回 token 后直接登录（走 ServerSession.Register，它内部就完成了登录），
/// 然后自动关闭本页回到主界面。首页账户区由 ServerSession.SessionChanged 驱动刷新，
/// 不需要这里再去通知谁。
///
/// 「确认密码」只在本地比对，不发给服务端：服务端本来就只收一个 password，
/// 多传一个字段纯属给自己找麻烦。
/// </summary>
public class RegisterPageUITKController : MonoBehaviour
{
    [SerializeField] private UIDocument document;
    [SerializeField] private LoginPageUITKController loginPage;

    private TextField usernameField;
    private TextField passwordField;
    private TextField confirmField;
    private Label stateLabel;
    private Label messageLabel;
    private Button submitButton;
    private Button backButton;

    private bool bindScheduled;
    private bool busy;

    // 登录页 ↔ 注册页来回切时保留账号，省得重新敲一遍
    private string pendingUsername;

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
    /// 激活注册页对象并安排一次界面绑定与刷新。
    /// </summary>
    public void Open()
    {
        gameObject.SetActive(true);
        ScheduleBind();
    }

    /// <summary>
    /// 隐藏注册页对象，把界面交还给首页主界面。
    /// </summary>
    public void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>预先填好账号（比如从登录页带过来）。绑定完成前调用也没问题。</summary>
    public void SetUsername(string username)
    {
        pendingUsername = username ?? string.Empty;

        if (usernameField != null)
            usernameField.value = pendingUsername;
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
    /// 取到输入框与按钮后设置密码遮挡与长度上限、回填账号并挂接回调。
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

        usernameField = root.Q<TextField>("UsernameField");
        passwordField = root.Q<TextField>("PasswordField");
        confirmField = root.Q<TextField>("ConfirmField");
        stateLabel = root.Q<Label>("StateLabel");
        messageLabel = root.Q<Label>("MessageLabel");
        submitButton = root.Q<Button>("SubmitButton");
        backButton = root.Q<Button>("BackButton");

        // UXML 还没建好（AssetDatabase 首帧延迟），下一帧再来
        if (usernameField == null || submitButton == null)
        {
            ScheduleBind();
            return;
        }

        // 密码框遮挡由代码设置，避免依赖 UXML 属性名在不同 Unity 版本上的差异
        passwordField.isPasswordField = true;
        passwordField.maxLength = 64;
        confirmField.isPasswordField = true;
        confirmField.maxLength = 64;
        usernameField.maxLength = 24;

        // 密码与确认密码不参与自动大写/自动纠正，避免移动端把首字符改掉
        usernameField.value = pendingUsername ?? string.Empty;
        passwordField.value = string.Empty;
        confirmField.value = string.Empty;

        BindClick(submitButton, OnSubmitClick);
        BindClick(backButton, OnBackClick);
        BindClick(root.Q<Button>("CloseButton"), OnCloseClick);

        RefreshState();
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
    /// 校验输入并确认两次密码一致后发起注册请求。
    /// </summary>
    private void OnSubmitClick()
    {
        if (!TryCollectCredentials(out string username, out string password, out string confirm)) return;

        if (password != confirm)
        {
            SetMessage("两次输入的密码不一致。", MessageKind.Error);
            return;
        }

        PlayClickSafely();
        Submit(username, password);
    }

    /// <summary>
    /// 把已填账号带回登录页并打开它，同时关闭当前注册页。
    /// </summary>
    private void OnBackClick()
    {
        PlayClickSafely();

        if (loginPage != null)
        {
            loginPage.SetUsername(usernameField != null ? usernameField.value : string.Empty);
            loginPage.Open();
        }

        Close();
    }

    /// <summary>
    /// 播放点击音效后关闭注册页回到首页。
    /// </summary>
    private void OnCloseClick()
    {
        PlayClickSafely();
        Close();
    }

    /// <summary>
    /// 读取并校验账号与两次密码，不合规时在页面上提示并返回失败。
    /// </summary>
    private bool TryCollectCredentials(out string username, out string password, out string confirm)
    {
        username = string.Empty;
        password = string.Empty;
        confirm = string.Empty;

        if (busy) return false;
        if (usernameField == null || passwordField == null || confirmField == null) return false;

        username = usernameField.value?.Trim() ?? string.Empty;
        password = passwordField.value ?? string.Empty;
        confirm = confirmField.value ?? string.Empty;

        if (username.Length == 0)
        {
            SetMessage("请输入账号。", MessageKind.Error);
            return false;
        }

        if (username.Length < 3)
        {
            SetMessage("账号至少 3 位。", MessageKind.Error);
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
    /// 禁用按钮并发起注册，注册成功即完成登录，刷新金币后关闭页面。
    /// </summary>
    private void Submit(string username, string password)
    {
        busy = true;
        SetInteractive(false);
        SetMessage("正在注册…", MessageKind.Normal);

        ServerSession.Register(username, password, (ok, message) =>
        {
            busy = false;
            SetInteractive(true);

            if (!ok)
            {
                SetMessage(message, MessageKind.Error);
                return;
            }

            // 注册即登录：此时 DataStoreLocator 已经切到 ApiDataStore，
            // 首页账户区也已通过 SessionChanged 刷新，直接关掉本页回主界面。
            SetMessage(message, MessageKind.Success);
            CoinDisplayManager.UpdateAll();
            Close();
        });
    }

    /// <summary>
    /// 在请求进行中禁用或恢复注册与返回按钮的点击。
    /// </summary>
    private void SetInteractive(bool interactive)
    {
        if (submitButton != null) submitButton.SetEnabled(interactive);
        if (backButton != null) backButton.SetEnabled(interactive);
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
