using UnityEngine.Pool;
using UnityEngine;
public class ThrowablePool : Pool
{
    protected ObjectPool<Throwable> pool;

    /// <summary>
    /// 保存池参数并创建投掷物对象池。
    /// </summary>
    public override void Init(EntityManager entityManager, Character playerCharacter, GameObject prefab, bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 10000)
    {
        base.Init(entityManager, playerCharacter, prefab, collectionCheck, defaultCapacity, maxSize);
        pool = new ObjectPool<Throwable>(CreatePooledItem, OnTakeFromPool, OnReturnedToPool, OnDestroyPooledItem, collectionCheck, defaultCapacity, maxSize);
    }

    /// <summary>
    /// 从对象池取出一个投掷物。
    /// </summary>
    public Throwable Get()
    {
        return pool.Get();
    }

    /// <summary>
    /// 把投掷物归还对象池。
    /// </summary>
    public void Release(Throwable throwable)
    {
        pool.Release(throwable);
    }

    /// <summary>
    /// 池中无可用对象时实例化投掷物并完成初始化。
    /// </summary>
    protected Throwable CreatePooledItem()
    {
        Throwable throwable = Instantiate(prefab, transform).GetComponent<Throwable>();
        throwable.Init(entityManager, playerCharacter);
        return throwable;
    }

    /// <summary>
    /// 取出时激活投掷物的 GameObject。
    /// </summary>
    protected void OnTakeFromPool(Throwable throwable)
    {
        throwable.gameObject.SetActive(true);
    }

    /// <summary>
    /// 回收时隐藏投掷物的 GameObject。
    /// </summary>
    protected void OnReturnedToPool(Throwable throwable)
    {
        throwable.gameObject.SetActive(false);
    }

    /// <summary>
    /// 池销毁或超容量时真正销毁投掷物的 GameObject。
    /// </summary>
    protected void OnDestroyPooledItem(Throwable throwable)
    {
        Destroy(throwable.gameObject);
    }
}
