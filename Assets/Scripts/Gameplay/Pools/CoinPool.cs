using UnityEngine.Pool;
using UnityEngine;
public class CoinPool : Pool
{
    protected ObjectPool<Coin> pool;

    /// <summary>
    /// 保存池参数并创建金币对象池。
    /// </summary>
    public override void Init(EntityManager entityManager, Character playerCharacter, GameObject prefab, bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 10000)
    {
        base.Init(entityManager, playerCharacter, prefab, collectionCheck, defaultCapacity, maxSize);
        pool = new ObjectPool<Coin>(CreatePooledItem, OnTakeFromPool, OnReturnedToPool, OnDestroyPooledItem, collectionCheck, defaultCapacity, maxSize);
    }

    /// <summary>
    /// 从对象池取出一个金币。
    /// </summary>
    public Coin Get()
    {
        return pool.Get();
    }

    /// <summary>
    /// 把金币归还对象池。
    /// </summary>
    public void Release(Coin coin)
    {
        pool.Release(coin);
    }

    /// <summary>
    /// 池中无可用对象时实例化金币并完成初始化。
    /// </summary>
    protected Coin CreatePooledItem()
    {
        Coin coin = Instantiate(prefab, transform).GetComponent<Coin>();
        coin.Init(entityManager, playerCharacter);
        return coin;
    }

    /// <summary>
    /// 取出时激活金币的 GameObject。
    /// </summary>
    protected void OnTakeFromPool(Coin coin)
    {
        coin.gameObject.SetActive(true);
    }

    /// <summary>
    /// 回收时隐藏金币的 GameObject。
    /// </summary>
    protected void OnReturnedToPool(Coin coin)
    {
        coin.gameObject.SetActive(false);
    }

    /// <summary>
    /// 池销毁或超容量时真正销毁金币的 GameObject。
    /// </summary>
    protected void OnDestroyPooledItem(Coin coin)
    {
        Destroy(coin.gameObject);
    }
}
