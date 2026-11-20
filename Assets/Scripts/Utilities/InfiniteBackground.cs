using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class InfiniteBackground : MonoBehaviour
{
    private Transform playerTransform;
    [SerializeField] private Material backgroundMaterial;
    private Material materialInstance;  // 运行时材质实例，避免直接改 .mat 资产留下脏修改
    private Vector2 previousResetPosition = Vector2.zero;
    private Vector2 resetOffset = Vector2.zero;
    private float resetDistance = 15;
    private float resetDuration = 5;
    [Tooltip("背景四边形比屏幕大多少倍。留余量给屏幕震动，否则相机抖动时边缘会露出黑边")]
    [SerializeField] private float screenMargin = 1.6f;

    /// <summary>
    /// 缓存材质并初始化背景平铺参数。
    /// </summary>
    void Awake()
    {
        // 计算屏幕在世界空间中的尺寸，用于适配背景大小
        Vector2 bottomLeft = Camera.main.ViewportToWorldPoint(new Vector3(0, 0, Camera.main.nearClipPlane));
        Vector2 topRight = Camera.main.ViewportToWorldPoint(new Vector3(1, 1, Camera.main.nearClipPlane));
        Vector3 screenSizeWorldSpace = new Vector3(topRight.x - bottomLeft.x, topRight.y - bottomLeft.y, 1);
        // 着色器的 UV 用的是世界坐标，放大四边形只是多画一点地面，不会让地面缩放变形
        transform.localScale = screenSizeWorldSpace * Mathf.Max(1f, screenMargin);
        // 运行时改用材质实例：Init/SetBackground/Shockwave 都会改材质属性，
        // 直接改序列化引用的 .mat 资产会在编辑器里残留脏修改（退出 Play 不还原）
        if (Application.isPlaying && backgroundMaterial != null)
        {
            materialInstance = new Material(backgroundMaterial);
            backgroundMaterial = materialInstance;
        }
        GetComponent<MeshRenderer>().sharedMaterial = backgroundMaterial;
    }

    /// <summary>
    /// 设置背景贴图与跟随目标。
    /// </summary>
    public void Init(Texture2D backgroundTexture, Transform playerTransform)
    {
        this.playerTransform = playerTransform;
        backgroundMaterial.mainTexture = backgroundTexture;
        backgroundMaterial.SetFloat("_Shockwave", 0);
        resetOffset = Vector2.zero;
        backgroundMaterial.SetVector("_ResetOffset", resetOffset);
        backgroundMaterial.SetInt("_Resetting", 0);
    }

    /// <summary>
    /// 相位切换时只换背景贴图：不重建对象、不动 resetOffset 与重置流程，
    /// 单帧开销只是一次贴图引用赋值，可忽略。
    /// </summary>
    public void SetBackground(Texture2D backgroundTexture)
    {
        if (backgroundTexture == null || backgroundMaterial == null) return;
        backgroundMaterial.mainTexture = backgroundTexture;
    }

    /// <summary>
    /// 销毁时释放运行时创建的材质与贴图。
    /// </summary>
    private void OnDestroy()
    {
        // 材质实例跟随组件销毁，防止泄漏
        if (materialInstance != null)
        {
            Destroy(materialInstance);
            materialInstance = null;
        }
    }

    /// <summary>
    /// 播放一次扩散冲击波（吸取金币与经验时的表现）。
    /// </summary>
    public IEnumerator Shockwave(float distance)
    {
        float d = 0;
        while (d < distance)
        {
            d += Time.deltaTime*16;
            backgroundMaterial.SetFloat("_Shockwave", d);
            backgroundMaterial.SetVector("_PlayerPosition", playerTransform.position);
            yield return null;
        }
        backgroundMaterial.SetFloat("_Shockwave", 0);
    }
    
    /// <summary>
    /// 按玩家位置滚动背景，超出范围时触发重置。
    /// </summary>
    private void Update()
    {
        // Init 可能在同帧晚于本次 Update 执行（Start 顺序不保证），玩家引用未就绪时先跳过
        if (playerTransform == null) return;
        Vector2 toReset = previousResetPosition - (Vector2)playerTransform.position;
        if (toReset.sqrMagnitude > resetDistance * resetDistance)
        {
            StartCoroutine(ResetBackground(toReset));

            previousResetPosition = playerTransform.position;
        }
    }

    /// <summary>
    /// 把越界的那块背景搬到另一侧，实现无限滚动。
    /// </summary>
    private IEnumerator ResetBackground(Vector2 toReset)
    {
        backgroundMaterial.SetInt("_Resetting", 1);
        backgroundMaterial.SetVector("_TempResetOffset", toReset);
        
        float t = 0;
        while (t < resetDuration)
        {
            t += Time.deltaTime;
            backgroundMaterial.SetFloat("_ResetBlend", t/resetDuration);
            yield return null;
        }
        
        // 更新重置偏移量
        resetOffset += toReset;
        backgroundMaterial.SetVector("_ResetOffset", resetOffset);
        backgroundMaterial.SetInt("_Resetting", 0);
    }
}
