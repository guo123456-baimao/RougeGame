using System.Collections;
using UnityEngine;
public enum GemType 
{
    White1 = 1,
    Blue2 = 2,
    Green10 = 10,
    Red50 = 50
}

public class ExpGem : Collectable
{
    [Header("Gem Dependencies")]
    [SerializeField] protected ExpGemBlueprint gemBlueprint;
    protected GemType gemType;
    protected TrailRenderer trailRenderer;
    protected SpriteRenderer spriteRenderer;

    public TrailRenderer TrailRenderer { get => trailRenderer; }

    /// <summary>
    /// 缓存拖尾与精灵渲染器。
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        trailRenderer = GetComponent<TrailRenderer>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    /// <summary>
    /// 设置宝石类型、贴图与拖尾颜色，并播放出现动画。
    /// </summary>
    public void Setup(Vector2 position, GemType gemType = GemType.White1, bool spawnAnimation = true)
    {
        trailRenderer.emitting = false;
        transform.position = position;
        trailRenderer.emitting = true;
        this.gemType = gemType;
        (Sprite sprite, Color color) = gemBlueprint.gemSpritesAndColors[gemType];
        spriteRenderer.sprite = sprite;
        trailRenderer.startColor = color;
        trailRenderer.endColor = color;
        base.Setup(spawnAnimation);
    }

    protected override string PickupLabel => "+" + (int)gemType + " EXP";
    protected override Color PickupColor => HudTheme.Active.expColor;

    /// <summary>
    /// 为角色增加对应经验值，并回收宝石。
    /// </summary>
    protected override void OnCollected()
    {
        spriteRenderer.enabled = false;
        playerCharacter.GainExp((float)gemType);
        AudioManager.Instance.PlayCollectXP();
        entityManager.DespawnGem(this);
        spriteRenderer.enabled = true;
    }
}
