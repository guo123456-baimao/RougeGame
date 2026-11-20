using UnityEngine.Pool;
using UnityEngine;
public class DamageTextPool : Pool
{
    protected ObjectPool<DamageText> pool;

    /// <summary>
    /// 保存池参数并创建飘字对象池。
    /// </summary>
    public override void Init(EntityManager entityManager, Character playerCharacter, GameObject prefab, bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 10000)
    {
        base.Init(entityManager, playerCharacter, prefab, collectionCheck, defaultCapacity, maxSize);
        pool = new ObjectPool<DamageText>(CreatePooledItem, OnTakeFromPool, OnReturnedToPool, OnDestroyPooledItem, collectionCheck, defaultCapacity, maxSize);
    }

    /// <summary>
    /// 从对象池取出一个飘字。
    /// </summary>
    public DamageText Get()
    {
        return pool.Get();
    }

    /// <summary>
    /// 把飘字归还对象池。
    /// </summary>
    public void Release(DamageText text)
    {
        pool.Release(text);
    }

    /// <summary>
    /// 池中无可用对象时实例化飘字并完成初始化。
    /// </summary>
    protected DamageText CreatePooledItem()
    {
        DamageText text = Instantiate(prefab, transform).GetComponent<DamageText>();
        text.Init(entityManager);
        return text;
    }

    /// <summary>
    /// 取出时激活飘字的 GameObject。
    /// </summary>
    protected void OnTakeFromPool(DamageText text)
    {
        text.gameObject.SetActive(true);
    }

    /// <summary>
    /// 回收时隐藏飘字的 GameObject。
    /// </summary>
    protected void OnReturnedToPool(DamageText text)
    {
        text.gameObject.SetActive(false);
    }

    /// <summary>
    /// 池销毁或超容量时真正销毁飘字的 GameObject。
    /// </summary>
    protected void OnDestroyPooledItem(DamageText text)
    {
        Destroy(text.gameObject);
    }
}
