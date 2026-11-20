using System;
using UnityEngine;
/// <summary>
/// 登录态管理：保存 token、恢复会话、退出账号。
///
/// 唯一允许碰 token 的地方。token 是"钥匙"而不是存档：
/// 丢了重新登录就行，所以存在 PlayerPrefs 里不违反"存档不上本地"的原则。
/// </summary>
public static class ServerSession
{
    private const string TokenKey = "Auth_Token";
    private const string UsernameKey = "Auth_Username";

    public static string Token { get; private set; }
    public static string Username { get; private set; }

    public static bool IsLoggedIn => !string.IsNullOrEmpty(Token);

    /// <summary>
    /// 登录态变化（登录 / 注册 / 退出 / 会话恢复失败）。
    /// 触发时机保证在 DataStoreLocator 已经换好的之后，订阅者可以安全地读新数据源。
    /// </summary>
    public static event Action SessionChanged;

    /// <summary>
    /// 从 PlayerPrefs 恢复上次的登录态（token 与用户名）。
    /// </summary>
    static ServerSession()
    {
        Token = PlayerPrefs.GetString(TokenKey, string.Empty);
        Username = PlayerPrefs.GetString(UsernameKey, string.Empty);
    }

    /// <summary>
    /// 把 token 与用户名写入内存并持久化到 PlayerPrefs。
    /// </summary>
    private static void Persist(string token, string username)
    {
        Token = token;
        Username = username;
        PlayerPrefs.SetString(TokenKey, token);
        PlayerPrefs.SetString(UsernameKey, username);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 清空内存与 PlayerPrefs 中的登录态。
    /// </summary>
    private static void ClearPersisted()
    {
        Token = string.Empty;
        Username = string.Empty;
        PlayerPrefs.DeleteKey(TokenKey);
        PlayerPrefs.DeleteKey(UsernameKey);
        PlayerPrefs.Save();
    }

    /// <summary>注册，成功顺便登录并切换到联网存储。</summary>
    public static void Register(string username, string password, Action<bool, string> onDone)
    {
        var client = new ApiClient();
        client.Register(username, password, envelope =>
        {
            if (envelope == null)
            {
                onDone?.Invoke(false, "网络异常，连不上服务器");
                return;
            }
            if (envelope.code != 0)
            {
                onDone?.Invoke(false, envelope.message);
                return;
            }

            FinishLogin(envelope, username, onDone);
        });
    }

    /// <summary>登录，成功后 DataStoreLocator 整体切到 ApiDataStore。</summary>
    public static void Login(string username, string password, Action<bool, string> onDone)
    {
        var client = new ApiClient();
        client.Login(username, password, envelope =>
        {
            if (envelope == null)
            {
                onDone?.Invoke(false, "网络异常，连不上服务器");
                return;
            }
            if (envelope.code != 0)
            {
                onDone?.Invoke(false, envelope.message);
                return;
            }

            FinishLogin(envelope, username, onDone);
        });
    }

    /// <summary>有本地 token 时直接恢复会话（不需要重新输密码）。</summary>
    public static void TryRestoreSession(Action<bool, string> onDone)
    {
        if (!IsLoggedIn)
        {
            // 没登录也要走一遍换源流程：把本地存档里未结算的本局进度读回内存
            ApplyStore(new PlayerPrefsDataStore());
            onDone?.Invoke(false, "未登录，继续使用本地存储");
            return;
        }

        InstallApiStore(Token, onDone, silentFailure: true);
    }

    /// <summary>退出账号：清 token + 换回本地存储（本局进度各自留在自己的存档里）。</summary>
    public static void Logout()
    {
        ClearPersisted();
        ApplyStore(new PlayerPrefsDataStore());
        NotifySessionChanged();
    }

    /// <summary>
    /// 换数据源的标准三步：清掉上一个身份的内存态 → 换 store → 把新身份的本局进度捞回来。
    ///
    /// 只清不捞：本人重登后发现刚买的商品没了（金币已经扣了）；
    /// 只捞不清：上一个人的本局购买被下一个账号（以及离线存档）继承，就是串号。
    /// 这里刻意不调 ShopSession.Clear —— 那会把"清空"写进"当前"存档，
    /// 在切换前调用等于把上一个身份的记录抹掉。本局数据只在购买时和局末落盘。
    /// </summary>
    private static void ApplyStore(IDataStore next)
    {
        RunState.Clear();                          // 本局已购商品名 + 属性加成 + 待投放消耗品
        CrossSceneData.CharacterBlueprint = null;  // 上个身份选中的角色不能带过来
        CrossSceneData.EntityManager = null;

        DataStoreLocator.SetStore(next);
        ShopSession.LoadFromStore();
    }

    /// <summary>
    /// 广播会话变化，通知界面刷新登录状态。
    /// </summary>
    private static void NotifySessionChanged()
    {
        var handler = SessionChanged;
        if (handler != null)
            handler.Invoke();
    }

    /// <summary>
    /// 校验登录响应中的 token，持久化后切换到联网存储。
    /// </summary>
    private static void FinishLogin(AuthEnvelope envelope, string username, Action<bool, string> onDone)
    {
        if (envelope?.data == null || string.IsNullOrEmpty(envelope.data.accessToken))
        {
            onDone?.Invoke(false, "服务器返回异常：缺少 token");
            return;
        }

        Persist(envelope.data.accessToken, username);
        InstallApiStore(envelope.data.accessToken, onDone, silentFailure: false);
    }

    /// <summary>
    /// 用 token 拉取账号档案并切换为 ApiDataStore，拉取失败则回退本地存储。
    /// </summary>
    private static void InstallApiStore(string token, Action<bool, string> onDone, bool silentFailure)
    {
        var client = new ApiClient(token);
        client.GetProfile(envelope =>
        {
            if (envelope == null || envelope.code != 0)
            {
                if (silentFailure) ClearPersisted();
                else Persist(string.Empty, string.Empty);

                // 拿不到档案就等于没登录：必须切回本地存储，
                // 否则 DataStoreLocator 还停在上一份 ApiDataStore 上，界面读到的仍是别人的镜像数据。
                ApplyStore(new PlayerPrefsDataStore());

                // 通知界面回到未登录态
                NotifySessionChanged();
                onDone?.Invoke(false, envelope == null ? "网络异常，连不上服务器" : envelope.message);
                return;
            }

            var store = new ApiDataStore(client);
            store.ApplyServerProfile(envelope.data);

            // 换身份：先丢掉上一个身份的内存态，再把新账号存档里的本局进度捞回来
            ApplyStore(store);

            // 数据源已经换好，这时才通知界面刷新（比如首页显示账户名）
            NotifySessionChanged();

            var data = envelope.data;
            string detail = $"金币={data.coins} 角色={ToListLength(data.characters)} " +
                            $"铭文={ToListLength(data.engravings?.unlocked)} 装备={ToListLength(data.engravings?.selected)}";
            onDone?.Invoke(true, "云存档已加载：" + detail);
        });
    }

    private static int ToListLength(System.Array array) => array == null ? 0 : array.Length;
}
