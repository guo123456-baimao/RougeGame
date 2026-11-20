using UnityEngine.Pool;
using UnityEngine;
public class MonsterPool : Pool
{
    protected ObjectPool<Monster> pool;

    /// <summary>
    /// 保存池参数并创建怪物对象池。
    /// </summary>
    public override void Init(EntityManager entityManager, Character playerCharacter, GameObject prefab, bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 10000)
    {
        base.Init(entityManager, playerCharacter, prefab, collectionCheck, defaultCapacity, maxSize);
        pool = new ObjectPool<Monster>(CreatePooledItem, OnTakeFromPool, OnReturnedToPool, OnDestroyPooledItem, collectionCheck, defaultCapacity, maxSize);
    }

    /// <summary>
    /// 从对象池取出一个怪物。
    /// </summary>
    public Monster Get()
    {
        return pool.Get();
    }

    /// <summary>
    /// 把怪物归还对象池。
    /// </summary>
    public void Release(Monster monster)
    {
        pool.Release(monster);
    }

    /// <summary>
    /// 池中无可用对象时实例化怪物并完成初始化。
    /// </summary>
    protected Monster CreatePooledItem()
    {
        Monster monster = Instantiate(prefab, transform).GetComponent<Monster>();
        monster.Init(entityManager, playerCharacter);
        return monster;
    }

    /// <summary>
    /// 取出时激活怪物的 GameObject。
    /// </summary>
    protected void OnTakeFromPool(Monster monster)
    {
        monster.gameObject.SetActive(true);
    }

    /// <summary>
    /// 回收时完整还原外观与状态，避免精英数据污染下一个普通怪。
    /// </summary>
    protected void OnReturnedToPool(Monster monster)
    {
        // 回收时完整还原（缩放 / 染色 / 材质 / 词缀 / HP），避免精英污染普通怪
        monster.OnDespawned();
        monster.gameObject.SetActive(false);
    }

    /// <summary>
    /// 池销毁或超容量时真正销毁怪物的 GameObject。
    /// </summary>
    protected void OnDestroyPooledItem(Monster monster)
    {
        Destroy(monster.gameObject);
    }
}
