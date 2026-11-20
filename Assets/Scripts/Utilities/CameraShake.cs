using UnityEngine;
/// <summary>
/// 屏幕震动：只抖相机的 x / y，不动 z 与正交尺寸，也不影响世界坐标逻辑。
/// 首次调用时自动挂到主相机上，无需改场景。
/// 执行顺序放到最后，保证在“相机跟随玩家”的脚本之后再叠加偏移；
/// 每帧开头会把上一帧叠加的偏移撤销掉，所以无论相机是静止还是被别的脚本每帧重设位置，都不会累积漂移。
/// </summary>
[DefaultExecutionOrder(10000)]
public class CameraShake : MonoBehaviour
{
    // 玩家技能（炸弹 / 火箭）爆炸用的轻量震动，射速快也不会晃得难受
    public const float LightStrength = 0.07f;
    public const float LightDuration = 0.12f;

    private static CameraShake instance;

    [SerializeField] private float maxStrength = 1f;

    private float remaining;
    private float total;
    private float strength;
    private Vector3 appliedOffset;
    private Vector3 lastApplied;

    /// <summary>
    /// 请求一次震动。同时存在多个请求时取「更强 + 更长」，不做振幅叠加，避免画面失控。
    /// strength：最大偏移的世界单位；duration：持续秒数。
    /// </summary>
    public static void Shake(float strength, float duration)
    {
        if (strength <= 0f || duration <= 0f)
            return;

        CameraShake target = EnsureInstance();
        if (target == null)
            return;

        float clamped = Mathf.Min(strength, Mathf.Max(0.01f, target.maxStrength));

        // 上一段已经播完：直接开新的一段。
        // total 必须一起重置，否则后面较短的震动会被上一次的时长“摊薄”成几乎看不见的幅度。
        if (target.remaining <= 0f)
        {
            target.strength = clamped;
            target.remaining = duration;
            target.total = duration;
            return;
        }

        // 正在震：取更强 + 更长，不叠加振幅
        target.strength = Mathf.Max(target.strength, clamped);
        target.remaining = Mathf.Max(target.remaining, duration);
        target.total = Mathf.Max(target.total, target.remaining);
    }

    /// <summary>轻微震动：玩家炸弹 / 火箭爆炸用，强度刻意压得很低。</summary>
    public static void ShakeLight()
    {
        Shake(LightStrength, LightDuration);
    }

    /// <summary>
    /// 确保场景中存在一个摄像机震动实例，没有则创建。
    /// </summary>
    private static CameraShake EnsureInstance()
    {
        if (instance != null)
            return instance;

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return null;

        instance = mainCamera.GetComponent<CameraShake>();
        if (instance == null)
            instance = mainCamera.gameObject.AddComponent<CameraShake>();
        return instance;
    }

    /// <summary>
    /// 给相机叠加随机偏移，并随时间衰减震动强度。
    /// </summary>
    private void LateUpdate()
    {
        // 注意：本工程的相机是玩家角色的子物体，父物体每帧都在动，
        // 所以偏移必须记在 localPosition 上（世界坐标每帧都不同，用它判断会永远判断成“被动过”，导致偏移逐帧累积、相机永久跑偏）。
        if (appliedOffset != Vector3.zero && transform.localPosition == lastApplied)
            transform.localPosition -= appliedOffset;
        appliedOffset = Vector3.zero;

        if (remaining <= 0f)
            return;

        remaining -= Time.unscaledDeltaTime;
        if (remaining <= 0f)
        {
            remaining = 0f;
            total = 0f;
            strength = 0f;
            lastApplied = transform.localPosition;
            return;
        }

        float falloff = remaining / Mathf.Max(0.0001f, total);
        float amplitude = strength * falloff * falloff; // 尾部快速收敛，收得干脆
        Vector2 offset = Random.insideUnitCircle * amplitude;
        appliedOffset = new Vector3(offset.x, offset.y, 0f);
        transform.localPosition += appliedOffset;
        lastApplied = transform.localPosition;
    }

    /// <summary>组件被关闭 / 对象销毁前把偏移还回去，避免相机停在偏移位置上。</summary>
    private void OnDisable()
    {
        if (appliedOffset != Vector3.zero)
        {
            transform.localPosition -= appliedOffset;
            appliedOffset = Vector3.zero;
        }
        remaining = 0f;
        total = 0f;
        strength = 0f;
    }
}
