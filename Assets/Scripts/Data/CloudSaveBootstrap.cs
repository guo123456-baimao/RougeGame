using UnityEngine;
/// <summary>
/// 启动即尝试恢复云端会话：有本地 token 就去服务器拉档案，
/// 拉到了就把 DataStoreLocator 切到 ApiDataStore（此后全游戏数据都走数据库）。
/// 没登录就保持 P1 的本地存储，游戏照样能玩。
///
/// 不需要拖到任何场景里，RuntimeInitializeOnLoadMethod 会自动创建。
/// </summary>
public class CloudSaveBootstrap : MonoBehaviour
{
    /// <summary>
    /// 游戏启动时触发云端存档初始化（只执行一次）。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void KickOff()
    {
        // 出包后排查问题时先看这行：确认包里连的是不是正确的服务器地址
        Debug.Log($"[CloudSave] 服务器地址 {ApiClient.BaseUrl}");

        ServerSession.TryRestoreSession((ok, message) =>
        {
            // 切换是异步的：拉档案这几帧内 UI 可能还在读本地 store，之后会自动纠正
            Debug.Log($"[CloudSave] {(ok ? "已联网" : "离线")}：{message}");
        });
    }
}
