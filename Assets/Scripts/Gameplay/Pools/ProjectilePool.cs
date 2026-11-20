using UnityEngine.Pool;
using UnityEngine;
public class ProjectilePool : Pool
{
    protected ObjectPool<Projectile> pool;

    /// <summary>
    /// 保存池参数并创建弹丸对象池。
    /// </summary>
    public override void Init(EntityManager entityManager, Character playerCharacter, GameObject prefab, bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 10000)
    {
        base.Init(entityManager, playerCharacter, prefab, collectionCheck, defaultCapacity, maxSize);
        pool = new ObjectPool<Projectile>(CreatePooledItem, OnTakeFromPool, OnReturnedToPool, OnDestroyPooledItem, collectionCheck, defaultCapacity, maxSize);
    }

    /// <summary>
    /// 从对象池取出一个弹丸。
    /// </summary>
    public Projectile Get()
    {
        return pool.Get();
    }

    /// <summary>
    /// 把弹丸归还对象池。
    /// </summary>
    public void Release(Projectile projectile)
    {
        pool.Release(projectile);
    }

    /// <summary>
    /// 池中无可用对象时实例化弹丸并完成初始化。
    /// </summary>
    protected Projectile CreatePooledItem()
    {
        Projectile projectile = Instantiate(prefab, transform).GetComponent<Projectile>();
        projectile.Init(entityManager, playerCharacter);
        return projectile;
    }

    /// <summary>
    /// 取出时激活弹丸的 GameObject。
    /// </summary>
    protected void OnTakeFromPool(Projectile projectile)
    {
        projectile.gameObject.SetActive(true);
    }

    /// <summary>
    /// 回收时隐藏弹丸的 GameObject。
    /// </summary>
    protected void OnReturnedToPool(Projectile projectile)
    {
        projectile.gameObject.SetActive(false);
    }

    /// <summary>
    /// 池销毁或超容量时真正销毁弹丸的 GameObject。
    /// </summary>
    protected void OnDestroyPooledItem(Projectile projectile)
    {
        Destroy(projectile.gameObject);
    }
}
