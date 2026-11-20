using UnityEngine;
public enum CoinType 
{
    Bronze1 = 1,
    Silver2 = 2,
    Gold5 = 5,
    Pouch30 = 30,
    Bag50 = 50
}

public class Coin : Collectable
{
    [Header("Coin Dependencies")]
    [SerializeField] protected CoinBlueprint coinBlueprint;
    protected SpriteRenderer spriteRenderer;
    protected CoinType coinType;
    public CoinType CoinType { get => coinType; }

    /// <summary>
    /// 缓存精灵渲染器。
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    /// <summary>
    /// 设置金币面额、贴图与位置，并播放出现动画。
    /// </summary>
    public void Setup(Vector2 position, CoinType coinType = CoinType.Bronze1, bool spawnAnimation = true, bool collectableDuringSpawn = true)
    {
        this.coinType = coinType;
        spriteRenderer.sprite = coinBlueprint.coinSprites[coinType];
        transform.position = position;
        base.Setup(spawnAnimation, collectableDuringSpawn);
    }

    protected override string PickupLabel => "+" + (int)coinType;
    protected override Color PickupColor => HudTheme.Active.coinColor;

    /// <summary>
    /// 拾取后回收金币，并按面额计入本局收益。
    /// </summary>
    protected override void OnCollected()
    {
        entityManager.DespawnCoin(this);
    }
}
