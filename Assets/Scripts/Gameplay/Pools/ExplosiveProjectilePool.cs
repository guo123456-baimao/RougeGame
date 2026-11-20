using UnityEngine.Pool;
using UnityEngine;
public class ExplosiveProjectilePool : Pool
{
    protected ObjectPool<ExplosiveProjectile> pool;

    /// <summary>
    /// 保存池参数并创建爆炸弹丸对象池。
    /// </summary>
    public override void Init(EntityManager entityManager, Character playerCharacter, GameObject prefab, bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 10000)
    {
        base.Init(entityManager, playerCharacter, prefab, collectionCheck, defaultCapacity, maxSize);
        pool = new ObjectPool<ExplosiveProjectile>(CreatePooledItem, OnTakeFromPool, OnReturnedToPool, OnDestroyPooledItem, collectionCheck, defaultCapacity, maxSize);
    }

    /// <summary>
    /// 从对象池取出一个爆炸弹丸。
    /// </summary>
    public ExplosiveProjectile Get()
    {
        return pool.Get();
    }

    /// <summary>
    /// 把爆炸弹丸归还对象池。
    /// </summary>
    public void Release(ExplosiveProjectile projectile)
    {
        pool.Release(projectile);
    }

    /// <summary>
    /// 池中无可用对象时实例化爆炸弹丸并完成初始化。
    /// </summary>
    protected ExplosiveProjectile CreatePooledItem()
    {
        ExplosiveProjectile projectile = Instantiate(prefab, transform).GetComponent<ExplosiveProjectile>();
        projectile.Init(entityManager, playerCharacter);
        return projectile;
    }

    /// <summary>
    /// 取出时激活爆炸弹丸的 GameObject。
    /// </summary>
    protected void OnTakeFromPool(ExplosiveProjectile projectile)
    {
        projectile.gameObject.SetActive(true);
    }

    /// <summary>
    /// 回收时隐藏爆炸弹丸的 GameObject。
    /// </summary>
    protected void OnReturnedToPool(ExplosiveProjectile projectile)
    {
        projectile.gameObject.SetActive(false);
    }

    /// <summary>
    /// 池销毁或超容量时真正销毁爆炸弹丸的 GameObject。
    /// </summary>
    protected void OnDestroyPooledItem(ExplosiveProjectile projectile)
    {
        Destroy(projectile.gameObject);
    }
}
