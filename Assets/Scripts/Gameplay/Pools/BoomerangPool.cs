using UnityEngine.Pool;
using UnityEngine;
public class BoomerangPool : Pool
{
    protected ObjectPool<Boomerang> pool;

    /// <summary>
    /// 保存池参数并创建回旋镖对象池。
    /// </summary>
    public override void Init(EntityManager entityManager, Character playerCharacter, GameObject prefab, bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 10000)
    {
        base.Init(entityManager, playerCharacter, prefab, collectionCheck, defaultCapacity, maxSize);
        pool = new ObjectPool<Boomerang>(CreatePooledItem, OnTakeFromPool, OnReturnedToPool, OnDestroyPooledItem, collectionCheck, defaultCapacity, maxSize);
    }

    /// <summary>
    /// 从对象池取出一个回旋镖。
    /// </summary>
    public Boomerang Get()
    {
        return pool.Get();
    }

    /// <summary>
    /// 把回旋镖归还对象池。
    /// </summary>
    public void Release(Boomerang boomerang)
    {
        pool.Release(boomerang);
    }

    /// <summary>
    /// 池中无可用对象时实例化回旋镖并完成初始化。
    /// </summary>
    protected Boomerang CreatePooledItem()
    {
        Boomerang boomerang = Instantiate(prefab, transform).GetComponent<Boomerang>();
        boomerang.Init(entityManager, playerCharacter);
        return boomerang;
    }

    /// <summary>
    /// 取出时激活回旋镖的 GameObject。
    /// </summary>
    protected void OnTakeFromPool(Boomerang boomerang)
    {
        boomerang.gameObject.SetActive(true);
    }

    /// <summary>
    /// 回收时隐藏回旋镖的 GameObject。
    /// </summary>
    protected void OnReturnedToPool(Boomerang boomerang)
    {
        boomerang.gameObject.SetActive(false);
    }

    /// <summary>
    /// 池销毁或超容量时真正销毁回旋镖的 GameObject。
    /// </summary>
    protected void OnDestroyPooledItem(Boomerang boomerang)
    {
        Destroy(boomerang.gameObject);
    }
}
