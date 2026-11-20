using UnityEngine;
using System.Collections;
public class Chest : MonoBehaviour
{
    protected ChestBlueprint chestBlueprint; 
    protected EntityManager entityManager;
    protected Character playerCharacter;
    protected ZPositioner zPositioner;
    protected Transform chestItemsParent;
    protected SpriteRenderer spriteRenderer;
    protected bool opened = false;

    /// <summary>
    /// 注入实体管理器、玩家与掉落物父节点，并创建 Z 轴排序组件。
    /// </summary>
    public void Init(EntityManager entityManager, Character playerCharacter, Transform chestItemsParent)
    {
        this.entityManager = entityManager;
        this.playerCharacter = playerCharacter;
        this.chestItemsParent = chestItemsParent;
        (zPositioner = gameObject.AddComponent<ZPositioner>()).Init(playerCharacter.transform);
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    /// <summary>
    /// 按蓝图重置宝箱外观与开启状态，并播放出现动画。
    /// </summary>
    public void Setup(ChestBlueprint chestBlueprint)
    {
        this.chestBlueprint = chestBlueprint;
        transform.localScale = Vector3.one;
        spriteRenderer.sprite = chestBlueprint.closedChest;
        opened = false;
        StartCoroutine(Appear());
    }

    /// <summary>
    /// 在宝箱位置生成掉落物；玩家亲手开启时立即拾取。
    /// </summary>
    private void SpawnLoot(Loot<GameObject> loot, bool openedByPlayer = true)
    {
        GameObject item = Instantiate(loot.item, chestItemsParent);
        item.transform.position = transform.position;
        transform.position += Vector3.back*0.001f;  // 把掉落物挪到宝箱前面
        Collectable collectable = item.GetComponent<Collectable>();
        collectable.Init(entityManager, playerCharacter);
        Coin coin = collectable as Coin;
        if (coin != null)
            coin.Setup(transform.position, loot.coinType, true, true);
        else
            collectable.Setup(true, true);
        // 玩家亲手打开时立即拾取该物品
        if (openedByPlayer)
            collectable.Collect(Collectable.CollectionMode.FromChest);
    }

    /// <summary>
    /// 开启宝箱（已开启则忽略），启动开箱流程。
    /// </summary>
    public void OpenChest(bool openedByPlayer = true)
    {
        if (!opened)
        {
            opened = true;
            StartCoroutine(Open(openedByPlayer));
        }
    }

    /// <summary>
    /// 这段代码写得很糟糕，改动时务必小心。
    /// </summary>
    private IEnumerator Open(bool openedByPlayer = true)
    {
        spriteRenderer.sprite = chestBlueprint.openingChest;
        bool spawnLoot = !chestBlueprint.abilityChest || !entityManager.AbilitySelectionDialog.HasAvailableAbilities();
        if (spawnLoot)
            SpawnLoot(chestBlueprint.lootTable.DropLootObject(), openedByPlayer);
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.sprite = chestBlueprint.openChest;
        if (!spawnLoot)
            entityManager.AbilitySelectionDialog.Open(false);
        yield return new WaitForSeconds(0.15f);
        float t = 0;
        while (t < 1.0f)
        {
            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, EasingUtils.EaseOutQuart(t));
            t += Time.deltaTime*2;
            yield return null;
        }
        entityManager.DespawnChest(this);
    }

    /// <summary>
    /// 出现动画：从 0 放大到 1，期间关闭碰撞体避免提前触发开启。
    /// </summary>
    private IEnumerator Appear()
    {
        GetComponent<Collider2D>().enabled = false;
        float t = 0;
        while (t < 1.0f)
        {
            transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, EasingUtils.EaseInQuart(t));
            t += Time.deltaTime*2;
            yield return null;
        }
        transform.localScale = Vector3.one;
        GetComponent<Collider2D>().enabled = true;
    }

    /// <summary>
    /// 玩家碰到宝箱时自动开启。
    /// </summary>
    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.collider.gameObject == playerCharacter.gameObject)
        {
            OpenChest();
        }
    }
}
