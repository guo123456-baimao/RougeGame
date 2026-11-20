using UnityEngine.Pool;
using UnityEngine;
public class ExpGemPool : Pool
{
    protected ObjectPool<ExpGem> pool;

    /// <summary>
    /// 保存池参数并创建经验宝石对象池。
    /// </summary>
    public override void Init(EntityManager entityManager, Character playerCharacter, GameObject prefab, bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 10000)
    {
        base.Init(entityManager, playerCharacter, prefab, collectionCheck, defaultCapacity, maxSize);
        pool = new ObjectPool<ExpGem>(CreatePooledItem, OnTakeFromPool, OnReturnedToPool, OnDestroyPooledItem, collectionCheck, defaultCapacity, maxSize);
    }

    /// <summary>
    /// 从对象池取出一个经验宝石。
    /// </summary>
    public ExpGem Get()
    {
        return pool.Get();
    }

    /// <summary>
    /// 把经验宝石归还对象池。
    /// </summary>
    public void Release(ExpGem expGem)
    {
        pool.Release(expGem);
    }

    /// <summary>
    /// 池中无可用对象时实例化经验宝石并完成初始化。
    /// </summary>
    protected ExpGem CreatePooledItem()
    {
        ExpGem expGem = Instantiate(prefab, transform).GetComponent<ExpGem>();
        expGem.Init(entityManager, playerCharacter);
        return expGem;
    }

    /// <summary>
    /// 取出时激活经验宝石的 GameObject。
    /// </summary>
    protected void OnTakeFromPool(ExpGem expGem)
    {
        expGem.gameObject.SetActive(true);
    }

    /// <summary>
    /// 回收时隐藏经验宝石的 GameObject。
    /// </summary>
    protected void OnReturnedToPool(ExpGem expGem)
    {
        expGem.gameObject.SetActive(false);
    }

    /// <summary>
    /// 池销毁或超容量时真正销毁经验宝石的 GameObject。
    /// </summary>
    protected void OnDestroyPooledItem(ExpGem expGem)
    {
        Destroy(expGem.gameObject);
    }
}
