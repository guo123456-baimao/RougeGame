using UnityEngine;
/// <summary>
/// 全局存储入口：业务代码统一通过 DataStoreLocator.Store 读写永久数据。
/// P1 默认本地 PlayerPrefs 实现；P3 接入数据库时在启动处调用
/// DataStoreLocator.SetStore(new ApiDataStore(...)) 即可整体切换，业务代码零改动。
/// </summary>
public static class DataStoreLocator
{
    private static IDataStore store;

    public static IDataStore Store
    {
        get
        {
            if (store == null)
                store = new PlayerPrefsDataStore();
            return store;
        }
    }

    /// <summary>替换存储实现（登录成功后切 ApiDataStore、测试注入 Mock 等场景用）。</summary>
    public static void SetStore(IDataStore implementation)
    {
        store = implementation;
    }

    /// <summary>
    /// 编辑器里每次 Play 都会域重载；若关闭了域重载，静态字段会跨 Play 残留，这里强制重置。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        store = null;
    }
}
