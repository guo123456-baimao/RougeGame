using UnityEngine.Pool;
using UnityEngine;
public class GravityProjectilePool : Pool
{
    protected ObjectPool<GravityProjectile> pool;

    /// <summary>
    /// 保存池参数并创建重力弹丸对象池。
    /// </summary>
    public override void Init(EntityManager entityManager, Character playerCharacter, GameObject prefab, bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 10000)
    {
        base.Init(entityManager, playerCharacter, prefab, collectionCheck, defaultCapacity, maxSize);
        pool = new ObjectPool<GravityProjectile>(CreatePooledItem, OnTakeFromPool, OnReturnedToPool, OnDestroyPooledItem, collectionCheck, defaultCapacity, maxSize);
    }

    /// <summary>
    /// 从对象池取出一个重力弹丸。
    /// </summary>
    public GravityProjectile Get()
    {
        return pool.Get();
    }

    /// <summary>
    /// 把重力弹丸归还对象池。
    /// </summary>
    public void Release(GravityProjectile projectile)
    {
        pool.Release(projectile);
    }

    /// <summary>
    /// 池中无可用对象时实例化重力弹丸并完成初始化。
    /// </summary>
    protected GravityProjectile CreatePooledItem()
    {
        GravityProjectile projectile = Instantiate(prefab, transform).GetComponent<GravityProjectile>();
        projectile.Init(entityManager, playerCharacter);
        return projectile;
    }

    /// <summary>
    /// 取出时激活重力弹丸的 GameObject。
    /// </summary>
    protected void OnTakeFromPool(GravityProjectile projectile)
    {
        projectile.gameObject.SetActive(true);
    }

    /// <summary>
    /// 回收时隐藏重力弹丸的 GameObject。
    /// </summary>
    protected void OnReturnedToPool(GravityProjectile projectile)
    {
        projectile.gameObject.SetActive(false);
    }

    /// <summary>
    /// 池销毁或超容量时真正销毁重力弹丸的 GameObject。
    /// </summary>
    protected void OnDestroyPooledItem(GravityProjectile projectile)
    {
        Destroy(projectile.gameObject);
    }
}
