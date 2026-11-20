using System.Collections;
using UnityEngine;
public abstract class Collectable : MonoBehaviour
{
    [Header("Type")]
    [field: SerializeField] public CollectableType CollectableType;
    [Header("Spawn Animation")]
    [SerializeField] protected float saSpeed = 1;
    [SerializeField] protected float saHeight = 1;
    [SerializeField] protected float saOffsetMax = 0.2f;
    [Header("Collect Animation")]
    [SerializeField] protected float juiciness = 2;
    [SerializeField] protected float lerpTime = 1;
    [Header("Attributes")]
    [SerializeField] protected bool magnetic = false;
    protected EntityManager entityManager;
    protected Character playerCharacter;
    protected ZPositioner zPositioner;
    protected Collider2D col;
    protected bool beingCollected = false;

    /// <summary>
    /// 缓存碰撞体并挂上 Z 轴排序组件。
    /// </summary>
    protected virtual void Awake()
    {
        col = GetComponent<Collider2D>();
        zPositioner = gameObject.AddComponent<ZPositioner>();
    }

    /// <summary>
    /// 注入实体管理器与玩家，并初始化 Z 轴排序。
    /// </summary>
    public virtual void Init(EntityManager entityManager, Character playerCharacter)
    {
        this.entityManager = entityManager;
        this.playerCharacter = playerCharacter;
        zPositioner.Init(playerCharacter.transform);
    }

    /// <summary>
    /// 重置拾取状态、注册到磁吸列表，并按需播放出现动画。
    /// </summary>
    public virtual void Setup(bool spawnAnimation = true, bool collectableDuringSpawn = true) {
        col.enabled = !spawnAnimation || collectableDuringSpawn;
        beingCollected = false;
        if (magnetic)
            entityManager.MagneticCollectables.Add(this);
        if (spawnAnimation)
            StartCoroutine(SpawnAnimation());
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 尝试拾取：去重、检查背包容量后飞向玩家或背包槽位。
    /// </summary>
    public virtual void Collect(CollectionMode collectionMode = CollectionMode.FromGround)
    {
        // 已经在被拾取中，不要重复拾取
        if (beingCollected)
            return;

        // 若该物品可放入背包且背包已满，则不拾取
        bool storeInInventory = CollectableType.inventoryStackSize > 0;
        bool hasInventorySlot = entityManager.Inventory.TryGetInventorySlot(this, out InventorySlot inventorySlot);
        if (storeInInventory && hasInventorySlot && inventorySlot.IsFull())
            return;
        
        // 标记为正在拾取，防止被重复拾取
        beingCollected = true;
        col.enabled = false;
        if (magnetic)
            entityManager.MagneticCollectables.Remove(this);

        // 可放入背包的物品飞向背包槽位，否则直接飞向玩家自身
        if (storeInInventory && hasInventorySlot)
            StartCoroutine(FlyToInventory(inventorySlot, collectionMode));
        else
            StartCoroutine(FlyToPlayer(collectionMode));
    }

    /// <summary>
    /// 直接结算该拾取物的效果（不播放飞行动画）。
    /// </summary>
    public void Use()
    {
        OnCollected();
    }

    /// <summary>
    /// 子类实现拾取后的具体效果。
    /// </summary>
    protected abstract void OnCollected();

    /// <summary>拾取时在角色头顶弹出的飘字内容，返回 null/空则不弹。</summary>
    protected virtual string PickupLabel => null;

    /// <summary>飘字颜色。默认走 HUD 主题的金币色。</summary>
    protected virtual Color PickupColor => HudTheme.Active.coinColor;

    /// <summary>在飘字时机上统一入口，子类只需覆写上面两个属性。</summary>
    protected void ShowPickupText()
    {
        string label = PickupLabel;
        if (string.IsNullOrEmpty(label)) return;
        Vector3 position = playerCharacter != null && playerCharacter.CenterTransform != null
            ? playerCharacter.CenterTransform.position
            : transform.position;
        InGameHud.Instance?.ShowPickupText(label, PickupColor, position);
    }

    /// <summary>
    /// 以回弹缓动飞向玩家，到达后播放音效、飘字并结算效果。
    /// </summary>
    protected virtual IEnumerator FlyToPlayer(CollectionMode collectionMode = CollectionMode.FromGround)
    {
        if (collectionMode == CollectionMode.FromChest)
        {
            yield return StartCoroutine(ChestAnimation());
            zPositioner.enabled = true;
        }

        float distance = Vector2.Distance(transform.position, playerCharacter.CenterTransform.position);
        if (distance == 0.0f) distance = Mathf.Epsilon;
        float c = juiciness / distance;
        float timeScale = 1.0f/(lerpTime*Mathf.Sqrt(distance));
        float t = -Time.deltaTime*timeScale;
        Vector3 pickupPos = transform.position;
        while (t < 1)
        {
            t += Time.deltaTime*timeScale;
            float lerpT = EasingUtils.EaseInBack(t, c);
            if (lerpT >= 1) break;
            transform.position = Vector3.LerpUnclamped(pickupPos, playerCharacter.CenterTransform.position, lerpT);
            yield return null;
        }
        transform.position = playerCharacter.CenterTransform.position;
        yield return null;
        AudioManager.Instance.PlayPickup();
        ShowPickupText();
        OnCollected();
    }

    /// <summary>
    /// 以回弹缓动飞向背包槽位，到达后写入背包并隐藏自身。
    /// </summary>
    protected virtual IEnumerator FlyToInventory(InventorySlot inventorySlot, CollectionMode collectionMode = CollectionMode.FromGround)
    {
        // 从宝箱中拾取时，先把掉落物抛到空中再送入背包
        inventorySlot.AddItemBeingCollected(this);
        if (collectionMode == CollectionMode.FromChest)
            yield return StartCoroutine(ChestAnimation());

        float t = 0;
        float c = 0;//juiciness / distance;
        float timeScale = 2.0f;
        t = -Time.deltaTime*timeScale;
        Vector3 pickupPos = transform.position;
        while (t < 1)
        {
            t += Time.deltaTime*timeScale;
            float lerpT = EasingUtils.EaseInBack(t, c);
            if (lerpT >= 1) break;
            transform.position = Vector3.LerpUnclamped(pickupPos, Camera.main.ScreenToWorldPoint(inventorySlot.transform.position), lerpT);
            yield return null;
        }
        transform.position = Camera.main.ScreenToWorldPoint(inventorySlot.transform.position);
        yield return null;
        gameObject.SetActive(false);
        inventorySlot.FinalizeAddItemBeingCollected(this);
    }

    /// <summary>
    /// 出现动画：向上弹跳并轻微横向漂移，期间关闭碰撞体。
    /// </summary>
    protected virtual IEnumerator SpawnAnimation()
    {
        zPositioner.enabled = false;
        float t = 0;
        float horizontalSpeed = Random.Range(-saOffsetMax, saOffsetMax);
        Vector3 spawnPosition = transform.position;
        while (!beingCollected && t < 1)
        {
            transform.position = spawnPosition + Vector3.up*EasingUtils.Bounce(t, saHeight) + Vector3.right*horizontalSpeed*t;
            t += Time.deltaTime*saSpeed;
            yield return null;
        }
        col.enabled = true;
        zPositioner.enabled = true;
    }

    /// <summary>
    /// 从宝箱弹出时播放的短促弹跳动画。
    /// </summary>
    protected virtual IEnumerator ChestAnimation()
    {
        float t = 0;
        float horizontalSpeed = Random.Range(-saOffsetMax, saOffsetMax);
        Vector3 spawnPosition = transform.position;
        while (t < 0.2f)
        {
            zPositioner.enabled = false;
            transform.position = spawnPosition + Vector3.up*EasingUtils.Bounce(t, saHeight) + Vector3.right*horizontalSpeed*t;
            t += Time.deltaTime*saSpeed;
            yield return null;
        }
    }

    /// <summary>
    /// 玩家拾取碰撞体持续接触时尝试拾取。
    /// </summary>
    void OnTriggerStay2D(Collider2D collider)
    {
        if (collider == playerCharacter.CollectableCollider)
        {
            Collect();
        }
    }

    public enum CollectionMode
    {
        FromGround,
        FromChest
    }
}
