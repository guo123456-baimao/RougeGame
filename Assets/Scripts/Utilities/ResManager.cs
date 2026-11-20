using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
/// <summary>
/// Addressables 统一资源管理器：句柄字典去重并发加载，引用计数归零才真正 Release。
/// 资源分两级：常驻资源（如 HUD 主题）永不随局释放；会话资源随对象池生命周期借用，
/// EntityManager 销毁（场景卸载）时统一 ReleaseSession。
/// </summary>
public static class ResManager
{
    private class CustomAsyncOperationHandle
    {
        public AsyncOperationHandle handle;
        public int referenceCount;
        public bool isPersistent;   // 常驻资源不参与 ReleaseSession
    }

    // key = 资源名 + "#" + 资源类型，同类型同地址只保留一个句柄
    private static readonly Dictionary<string, CustomAsyncOperationHandle> resDic = new Dictionary<string, CustomAsyncOperationHandle>();          //句柄字典

    /// <summary>加载失败事件（默认只打错误日志，调用方可订阅上报）。</summary>
    public static event Action<string> OnLoadFailed;

    ////////////////////////////////////////////////////////////////////////////////
    /// 加载
    ////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// 异步加载并持有引用。重复请求同一资源不会重复加载，只会追加引用计数；
    /// 未加载完成时回调会在完成时补发，已加载完成则立即回调。
    /// </summary>
    public static void AssetLoadAsync<T>(string assetName, Action<T> callBack, bool isPersistent = false) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(assetName))
        {
            Debug.LogError("[ResManager] 加载失败：资源名为空");
            return;
        }

        string keyName = assetName + "#" + typeof(T).Name;                      //资源名+资源类型
        CustomAsyncOperationHandle customHandle;

        if (!resDic.TryGetValue(keyName, out customHandle))
        {
            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(assetName);
            customHandle = new CustomAsyncOperationHandle { handle = handle, referenceCount = 0, isPersistent = isPersistent };
            resDic[keyName] = customHandle;
            handle.Completed += hd =>
            {
                if (hd.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError($"[ResManager] 资源加载失败：{assetName} ({hd.OperationException})");
                    resDic.Remove(keyName);
                    OnLoadFailed?.Invoke(assetName);
                    callBack?.Invoke(null);     //失败也回调（回传 null），避免协程等待方永久挂起
                }
            };
        }

        // 引用计数在「发起请求」时就 +1，保证 Release 与加载并发时不会漏计
        customHandle.referenceCount++;

        AsyncOperationHandle<T> typed = customHandle.handle.Convert<T>();
        if (typed.IsDone)                                   // 已经完成,直接回调
        {
            callBack?.Invoke(typed.Result);
        }
        else                                                // 未完成,等加载完再回调
        {
            typed.Completed += hd =>
            {
                if (hd.Status == AsyncOperationStatus.Succeeded)
                    callBack?.Invoke(hd.Result);
                else
                    callBack?.Invoke(null);     //失败也回调（回传 null），避免协程等待方永久挂起
            };
        }
    }

    /// <summary>常驻资源加载：整个应用生命周期只加载一次，不随会话释放。</summary>
    public static void LoadPersistentAsync<T>(string assetName, Action<T> callBack) where T : UnityEngine.Object
    {
        AssetLoadAsync(assetName, callBack, isPersistent: true);
    }

    /// <summary>
    /// 协程友好的等待式加载：yield 返回直到资源就绪，适合初始化链路里按序加载。
    /// </summary>
    public static IEnumerator LoadRoutine<T>(string assetName, Action<T> callBack = null, bool isPersistent = false) where T : UnityEngine.Object
    {
        T result = null;
        bool done = false;
        AssetLoadAsync<T>(assetName, r => { result = r; done = true; }, isPersistent);
        while (!done)
            yield return null;
        callBack?.Invoke(result);
    }

    ////////////////////////////////////////////////////////////////////////////////
    /// 释放
    ////////////////////////////////////////////////////////////////////////////////

    /// <summary>释放一次引用；计数归零时真正调用 Addressables.Release。</summary>
    public static void Release<T>(string assetName) where T : UnityEngine.Object
    {
        string keyName = assetName + "#" + typeof(T).Name;

        if (!resDic.ContainsKey(keyName))
        {
            Debug.LogWarning($"[ResManager] 释放失败：未持有资源 {assetName}");
            return;
        }

        resDic[keyName].referenceCount--;                       //减少引用计数
        if (resDic[keyName].referenceCount > 0)
            return;

        AsyncOperationHandle handle = resDic[keyName].handle;
        resDic.Remove(keyName);
        Addressables.Release(handle);                           //释放资源
    }

    /// <summary>
    /// 释放本会话借用的所有资源（怪物/掉落物预制体等）。
    /// 常驻资源（HudTheme 等）不受影响，在 EntityManager 销毁（场景卸载）时调用。
    /// </summary>
    public static void ReleaseSession()
    {
        List<string> keysToRelease = new List<string>();
        foreach (var kvp in resDic)
        {
            if (!kvp.Value.isPersistent)
                keysToRelease.Add(kvp.Key);
        }

        foreach (string keyName in keysToRelease)
        {
            AsyncOperationHandle handle = resDic[keyName].handle;
            resDic.Remove(keyName);
            Addressables.Release(handle);
        }

        if (keysToRelease.Count > 0)
            Debug.Log($"[ResManager] 会话资源已释放：{keysToRelease.Count} 项");
    }
}
