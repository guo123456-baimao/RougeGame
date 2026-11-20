using UnityEngine;
/// <summary>
/// 精英头顶血条：红色血条 + 紧接在后面的一条等长白色护盾条（护盾词缀专属）。
/// 全部用运行时生成的白色 sprite 拼出来，不新增美术资源；挂在怪物节点下，随怪物移动。
/// </summary>
public class EliteHealthBar : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private float barWidth = 0.85f;
    [SerializeField] private float barHeight = 0.1f;
    [Tooltip("红条与白条之间的间隙，0 表示两条紧贴")]
    [SerializeField] private float segmentGap = 0f;
    [Tooltip("血条离精灵顶部的世界距离")]
    [SerializeField] private float heightAboveHead = 0.18f;

    [Header("Colors")]
    [SerializeField] private Color healthColor = new Color(0.92f, 0.22f, 0.20f, 1f);
    [SerializeField] private Color shieldColor = new Color(0.96f, 0.97f, 1f, 1f);
    [SerializeField] private Color trackColor = new Color(0.06f, 0.06f, 0.09f, 0.85f);

    private Monster owner;
    private Transform track;
    private Transform healthFill;
    private Transform shieldFill;
    private SpriteRenderer shieldRenderer;
    private float spriteUnit = 0.04f;

    /// <summary>
    /// 创建一条挂在指定怪物身上的精英血条并完成构建。
    /// </summary>
    public static EliteHealthBar Create(Monster owner)
    {
        GameObject go = new GameObject("Elite Health Bar");
        go.transform.SetParent(owner.transform, false);
        EliteHealthBar bar = go.AddComponent<EliteHealthBar>();
        bar.owner = owner;
        bar.Build();
        return bar;
    }

    /// <summary>
    /// 用运行时生成的白色 sprite 依次搭建底槽、护盾条与血条。
    /// </summary>
    private void Build()
    {
        Sprite sprite = HudUiFactory.WhiteSprite;
        spriteUnit = Mathf.Max(0.0001f, sprite.bounds.size.x);

        SpriteRenderer source = owner != null ? owner.BodyRenderer : null;
        int layerId = source != null ? source.sortingLayerID : 0;
        int order = source != null ? source.sortingOrder : 0;

        track = CreateSegment("Track", trackColor, layerId, order + 15, sprite);
        // 白色护盾条排在红条后面一段
        shieldFill = CreateSegment("Shield", shieldColor, layerId, order + 16, sprite);
        shieldRenderer = shieldFill.GetComponent<SpriteRenderer>();
        healthFill = CreateSegment("Health", healthColor, layerId, order + 17, sprite);
    }

    /// <summary>
    /// 创建一个血条分段渲染器（底槽 / 护盾 / 血量）。
    /// </summary>
    private Transform CreateSegment(string name, Color color, int layerId, int order, Sprite sprite)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerID = layerId;
        renderer.sortingOrder = order;
        return go.transform;
    }

    /// <summary>Setup 时调用一次：对齐头顶、抵消精英的体型缩放，保证血条粗细不随体型变化。</summary>
    public void RefreshLayout()
    {
        if (owner == null)
            return;

        Transform host = owner.transform;
        Vector3 lossy = host.lossyScale;
        float sx = Mathf.Approximately(lossy.x, 0f) ? 1f : lossy.x;
        float sy = Mathf.Approximately(lossy.y, 0f) ? 1f : lossy.y;
        transform.localScale = new Vector3(1f / sx, 1f / sy, 1f);

        float worldTop = owner.SpriteTopWorldY + heightAboveHead;
        float localY = host.InverseTransformPoint(new Vector3(host.position.x, worldTop, host.position.z)).y;
        transform.localPosition = new Vector3(0f, localY, -0.05f);

        UpdateBar();
    }

    /// <summary>
    /// 每帧刷新血条；怪物不是精英或已死亡时隐藏。
    /// </summary>
    private void LateUpdate()
    {
        if (owner == null || !owner.IsElite || !owner.Alive)
        {
            gameObject.SetActive(false);
            return;
        }
        UpdateBar();
    }

    /// <summary>
    /// 按当前血量与护盾比例刷新各分段的宽度与位置。
    /// </summary>
    private void UpdateBar()
    {
        float healthRatio = owner.HealthRatio;
        float shieldRatio = owner.ShieldRatio;
        bool hasShield = owner.MaxShield > 0f;

        float totalWidth = hasShield ? barWidth * 2f + segmentGap : barWidth;
        float left = -totalWidth * 0.5f;

        SetSegment(track, totalWidth, barHeight, left + totalWidth * 0.5f, 0f);
        SetSegment(healthFill, barWidth * healthRatio, barHeight, left + barWidth * healthRatio * 0.5f, -0.005f);

        if (shieldRenderer != null)
            shieldRenderer.enabled = hasShield;
        if (hasShield)
        {
            float shieldWidth = barWidth * shieldRatio;
            SetSegment(shieldFill, shieldWidth, barHeight,
                left + barWidth + segmentGap + shieldWidth * 0.5f, -0.01f);
        }
    }

    /// <summary>
    /// 设置单个分段的宽度、高度与水平居中偏移。
    /// </summary>
    private void SetSegment(Transform segment, float width, float height, float centerX, float z)
    {
        if (segment == null)
            return;
        segment.localScale = new Vector3(Mathf.Max(0.0001f, width) / spriteUnit, height / spriteUnit, 1f);
        segment.localPosition = new Vector3(centerX, 0f, z);
    }
}
