using UnityEngine.Pool;
using UnityEngine;
public class ChestPool : Pool
{
    protected ObjectPool<Chest> pool;

    /// <summary>
    /// 保存池参数并创建宝箱对象池。
    /// </summary>
    public override void Init(EntityManager entityManager, Character playerCharacter, GameObject prefab, bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 10000)
    {
        base.Init(entityManager, playerCharacter, prefab, collectionCheck, defaultCapacity, maxSize);
        pool = new ObjectPool<Chest>(CreatePooledItem, OnTakeFromPool, OnReturnedToPool, OnDestroyPooledItem, collectionCheck, defaultCapacity, maxSize);
    }

    /// <summary>
    /// 从对象池取出一个宝箱。
    /// </summary>
    public Chest Get()
    {
        return pool.Get();
    }

    /// <summary>
    /// 把宝箱归还对象池。
    /// </summary>
    public void Release(Chest chest)
    {
        pool.Release(chest);
    }

    /// <summary>
    /// 池中无可用对象时实例化宝箱并完成初始化。
    /// </summary>
    protected Chest CreatePooledItem()
    {
        Chest chest = Instantiate(prefab, transform).GetComponent<Chest>();
        chest.Init(entityManager, playerCharacter, transform);
        return chest;
    }

    /// <summary>
    /// 取出时激活宝箱的 GameObject。
    /// </summary>
    protected void OnTakeFromPool(Chest chest)
    {
        chest.gameObject.SetActive(true);
    }

    /// <summary>
    /// 回收时隐藏宝箱的 GameObject。
    /// </summary>
    protected void OnReturnedToPool(Chest chest)
    {
        chest.gameObject.SetActive(false);
    }

    /// <summary>
    /// 池销毁或超容量时真正销毁宝箱的 GameObject。
    /// </summary>
    protected void OnDestroyPooledItem(Chest chest)
    {
        Destroy(chest.gameObject);
    }
}
