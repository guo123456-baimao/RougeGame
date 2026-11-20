using UnityEngine;
/// <summary>
/// 死亡爆炸的表现 + 结算。
/// 先在死亡位置画出爆炸范围（延迟时间内从中心扩散到满半径），给玩家躲避窗口，到时再结算伤害与击退。
/// 对象挂在场景根下而不是怪物节点下，所以怪物回池 / 失活不会打断延迟结算。
/// 圆形贴图是运行时生成的，不新增美术资源。
/// </summary>
public class DeathExplosionEffect : MonoBehaviour
{
    private static Sprite fillSprite;
    private static Sprite ringSprite;

    [SerializeField] private Color fillColor = new Color(1f, 0.32f, 0.16f, 0.26f);
    [SerializeField] private Color ringColor = new Color(1f, 0.62f, 0.20f, 0.95f);
    [SerializeField] private int sortingOrder = 40;

    private Character target;
    private Vector2 center;
    private float radius = 1f;
    private float damage;
    private float knockback;
    private float delay = 0.5f;

    private SpriteRenderer fillRenderer;
    private SpriteRenderer ringRenderer;
    private float timer;
    private bool exploded;

    /// <summary>
    /// 在指定位置创建一个死亡爆炸特效并完成参数初始化。
    /// </summary>
    public static void Spawn(Character target, Vector2 position, float radius, float damage, float knockback, float delay)
    {
        GameObject go = new GameObject("Elite Death Explosion");
        go.transform.position = new Vector3(position.x, position.y, -1f);
        DeathExplosionEffect effect = go.AddComponent<DeathExplosionEffect>();
        effect.Init(target, position, radius, damage, knockback, delay);
    }

    /// <summary>
    /// 保存半径、伤害、延迟等参数并搭建预警圈渲染器。
    /// </summary>
    private void Init(Character newTarget, Vector2 position, float newRadius, float newDamage, float newKnockback, float newDelay)
    {
        target = newTarget;
        center = position;
        radius = Mathf.Max(0.1f, newRadius);
        damage = newDamage;
        knockback = newKnockback;
        delay = Mathf.Max(0.01f, newDelay);

        fillRenderer = CreateRenderer("Fill", FillSprite, fillColor, sortingOrder);
        ringRenderer = CreateRenderer("Ring", RingSprite, ringColor, sortingOrder + 1);
        SetRadius(radius * 0.2f);
    }

    /// <summary>
    /// 创建一个用于绘制爆炸范围的渲染器节点。
    /// </summary>
    private SpriteRenderer CreateRenderer(string name, Sprite sprite, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer;
    }

    /// <summary>
    /// 预警阶段把圆圈扩散到满半径，到时引爆，随后逐渐淡出并销毁。
    /// </summary>
    private void Update()
    {
        float deltaTime = Time.deltaTime;
        timer += deltaTime;

        if (!exploded)
        {
            float progress = Mathf.Clamp01(timer / delay);
            SetRadius(Mathf.Lerp(radius * 0.2f, radius, progress));
            if (timer < delay)
                return;

            exploded = true;
            timer = 0f;
            Explode();
            return;
        }

        float fade = Mathf.Clamp01(timer / EliteAffixTuning.DeathExplosionFlashDuration);
        SetAlpha(1f - fade);
        SetRadius(radius * (1f + 0.12f * fade));
        if (fade >= 1f)
            Destroy(gameObject);
    }

    /// <summary>
    /// 播放震屏与音效，并对处于半径内的玩家结算伤害与击退。
    /// </summary>
    private void Explode()
    {
        // 爆炸本身抖一下屏幕，跟有没有炸到人无关
        CameraShake.Shake(EliteAffixTuning.DeathExplosionShakeStrength, EliteAffixTuning.DeathExplosionShakeDuration);
        // 自爆音效：爆炸瞬间播放，跟有没有炸到人无关（音效在 AudioManager 的 deathExplosion 字段配置）
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayDeathExplosion();
        if (target == null)
            return;

        Vector2 toTarget = (Vector2)target.transform.position - center;
        if (toTarget.sqrMagnitude > radius * radius)
            return;

        Vector2 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.up;
        target.TakeDamage(damage, direction * knockback);
    }

    /// <summary>
    /// 按「世界半径」设置预警圈大小。sprite 是直径 1 世界单位的圆，所以 scale = 2 × 半径，
    /// 这样圈画出来的范围与 Explode() 里的判定半径完全一致（以前直接拿半径当 scale，圈只有实际判定的一半大）。
    /// </summary>
    private void SetRadius(float worldRadius)
    {
        float diameter = Mathf.Max(0.02f, worldRadius * 2f);
        transform.localScale = new Vector3(diameter, diameter, 1f);
    }

    /// <summary>
    /// 同时调整填充与圆环的透明度。
    /// </summary>
    private void SetAlpha(float alpha)
    {
        if (fillRenderer != null)
        {
            Color color = fillColor;
            color.a *= alpha;
            fillRenderer.color = color;
        }
        if (ringRenderer != null)
        {
            Color color = ringColor;
            color.a *= alpha;
            ringRenderer.color = color;
        }
    }

    private static Sprite FillSprite
    {
        get
        {
            if (fillSprite == null)
                fillSprite = CreateCircleSprite(128, true);
            return fillSprite;
        }
    }

    private static Sprite RingSprite
    {
        get
        {
            if (ringSprite == null)
                ringSprite = CreateCircleSprite(128, false);
            return ringSprite;
        }
    }

    /// <summary>生成直径 1 世界单位的圆形贴图（实心或圆环），100% 程序生成。</summary>
    private static Sprite CreateCircleSprite(int size, bool filled)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half + 0.5f) / half;
                float dy = (y - half + 0.5f) / half;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha;
                if (filled)
                {
                    alpha = 1f - Mathf.SmoothStep(0.86f, 1f, distance);
                }
                else
                {
                    // 圆环压到贴图边缘（0.92~1.00），保证画出来的圈就是真实判定边界
                    float offset = Mathf.Abs(distance - 0.96f) / 0.04f;
                    alpha = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(offset));
                }

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(alpha * 255f, 0f, 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = filled ? "ExplosionFill" : "ExplosionRing";
        return sprite;
    }
}
