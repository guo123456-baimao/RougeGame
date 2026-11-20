using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
/// <summary>
/// 承载协程的隐藏 GameObject。Unity 的 UnityWebRequest 必须跑在协程里，
/// 而静态类没有协程能力，所以需要这么一个常驻对象。
/// </summary>
internal class ApiHttpRunner : MonoBehaviour
{
    private static ApiHttpRunner instance;

    public static ApiHttpRunner Instance
    {
        get
        {
            if (instance != null) return instance;

            var go = new GameObject("[ApiHttp]");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ApiHttpRunner>();
            return instance;
        }
    }
}

/// <summary>
/// 服务端 REST 客户端。每个方法都是"发起请求 + 回调"，内部走协程，
/// 不阻塞主线程，所以不会让游戏卡顿。
/// </summary>
public class ApiClient
{
    /// <summary>
    /// 后端地址。打包后不能写死，所以按以下顺序取：
    ///   1. exe 同级的 server.json     —— 出包后直接改这个，不用重新打包
    ///   2. StreamingAssets/server.json —— 随包发布的默认值
    ///   3. http://localhost:5000       —— 编辑器里连本机
    /// </summary>
    public static readonly string BaseUrl = ResolveBaseUrl();

    private const string DefaultBaseUrl = "http://localhost:5000";
    private const string ConfigFileName = "server.json";

    private const int TimeoutSeconds = 8;

    public string Token { get; private set; }

    public ApiClient(string token = null) { Token = token; }

    /// <summary>
    /// 向后端发起注册请求，结果通过回调返回。
    /// </summary>
    public void Register(string username, string password, Action<AuthEnvelope> callback)
    {
        Run(Send<AuthEnvelope>("POST", "/api/v1/auth/register",
            new AuthPayload { username = username, password = password }, callback));
    }

    /// <summary>
    /// 向后端发起登录请求，结果通过回调返回。
    /// </summary>
    public void Login(string username, string password, Action<AuthEnvelope> callback)
    {
        Run(Send<AuthEnvelope>("POST", "/api/v1/auth/login",
            new AuthPayload { username = username, password = password }, callback));
    }

    /// <summary>
    /// 拉取当前账号档案（金币、已拥有角色、铭文等）。
    /// </summary>
    public void GetProfile(Action<ProfileEnvelope> callback)
    {
        Run(Send<ProfileEnvelope>("GET", "/api/v1/profile", null, callback));
    }

    /// <summary>
    /// 把当前金币数写回服务器。
    /// </summary>
    public void SaveCoins(int coins, Action<CoinsEnvelope> callback)
    {
        Run(Send<CoinsEnvelope>("POST", "/api/v1/save/coins", new CoinsPayload { coins = coins }, callback));
    }

    /// <summary>
    /// 提交角色的购买状态到服务器。
    /// </summary>
    public void SaveCharacterOwned(string uid, bool owned, Action<BoolEnvelope> callback)
    {
        Run(Send<BoolEnvelope>("POST", "/api/v1/save/character-owned",
            new CharacterOwnedPayload { characterUid = uid, owned = owned }, callback));
    }

    /// <summary>
    /// 提交铭文解锁状态到服务器。
    /// </summary>
    public void UnlockEngraving(string engravingId, Action<BoolEnvelope> callback)
    {
        Run(Send<BoolEnvelope>("POST", "/api/v1/save/engraving-unlock",
            new EngravingUnlockPayload { engravingId = engravingId }, callback));
    }

    /// <summary>
    /// 提交当前装备的铭文列表到服务器。
    /// </summary>
    public void SelectEngravings(string[] engravingIds, Action<BoolEnvelope> callback)
    {
        Run(Send<BoolEnvelope>("POST", "/api/v1/save/engraving-select",
            new EngravingSelectPayload { engravingIds = engravingIds }, callback));
    }

    /// <summary>写入未结算的本局进度快照（传空串表示本局已结束）。</summary>
    public void SaveSessionSnapshot(string snapshot, Action<BoolEnvelope> callback)
    {
        Run(Send<BoolEnvelope>("POST", "/api/v1/save/session",
            new SessionSnapshotPayload { snapshot = snapshot ?? string.Empty }, callback));
    }

    /// <summary>服务端权威扣费。</summary>
    public void SpendCoins(SpendPayload payload, Action<SpendEnvelope> callback)
    {
        Run(Send<SpendEnvelope>("POST", "/api/v1/shop/purchase", payload, callback));
    }

    /// <summary>单局结算，服务端加息。</summary>
    public void SettleRun(int coinsGained, Action<CoinsEnvelope> callback)
    {
        Run(Send<CoinsEnvelope>("POST", "/api/v1/run/settle",
            new SettleRunPayload { coinsGained = coinsGained }, callback));
    }

    // ---- 内部实现 ----

    private static void Run(IEnumerator routine) => ApiHttpRunner.Instance.StartCoroutine(routine);

    private IEnumerator Send<T>(string method, string path, object payload, Action<T> callback) where T : class
    {
        string json = payload != null ? JsonUtility.ToJson(payload) : null;

        using (var request = BuildRequest(method, BaseUrl + path, json))
        {
            yield return request.SendWebRequest();

            bool transportOk = request.result == UnityWebRequest.Result.Success;
            string text = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

            if (!transportOk)
            {
                Debug.LogWarning($"[ApiClient] {method} {path} 请求失败：{request.error}");
                callback?.Invoke(null);
                yield break;
            }

            T result = null;
            if (!string.IsNullOrEmpty(text))
            {
                try { result = JsonUtility.FromJson<T>(text); }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ApiClient] {method} {path} 响应解析失败：{ex.Message}");
                }
            }

            callback?.Invoke(result);
        }
    }

    /// <summary>
    /// 按请求方法、地址与 JSON 体构建一个 UnityWebRequest。
    /// </summary>
    private UnityWebRequest BuildRequest(string method, string url, string json)
    {
        // GET 走纯下载，POST 需要上传原始 JSON
        var request = json == null
            ? new UnityWebRequest(url, method)
            : new UnityWebRequest(url, method)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json))
            };

        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = TimeoutSeconds;
        request.SetRequestHeader("Content-Type", "application/json");

        if (!string.IsNullOrEmpty(Token))
            request.SetRequestHeader("Authorization", "Bearer " + Token);

        return request;
    }

    // ---- 服务器地址配置 ----

    /// <summary>
    /// 决定后端地址：优先读取配置文件，否则使用默认地址。
    /// </summary>
    private static string ResolveBaseUrl()
    {
        // 1) exe 同级：出包后想换服务器地址，改这个文件就行，不用重新打包。
        //    Windows 包里 Application.dataPath 是 <exe目录>/<产品名>_Data，往上一级就是 exe 所在目录。
        string url = TryReadConfig(Path.Combine(Application.dataPath, "..", ConfigFileName));
        if (!string.IsNullOrEmpty(url)) return url;

        // 2) StreamingAssets：随包发布的默认值
        url = TryReadConfig(Path.Combine(Application.streamingAssetsPath, ConfigFileName));
        if (!string.IsNullOrEmpty(url)) return url;

        return DefaultBaseUrl;
    }

    /// <summary>
    /// 尝试读取指定路径的配置文件，失败时返回 null。
    /// </summary>
    private static string TryReadConfig(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;

            string url = JsonUtility.FromJson<ServerConfig>(File.ReadAllText(path))?.baseUrl;
            if (string.IsNullOrEmpty(url)) return null;

            return url.Trim().TrimEnd('/');
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ApiClient] 读取服务器配置 {path} 失败，已忽略：{ex.Message}");
            return null;
        }
    }
}
