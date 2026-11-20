using UnityEngine;
public class Pool : MonoBehaviour
{
    protected EntityManager entityManager;
    protected Character playerCharacter;
    protected GameObject prefab;
    protected bool collectionCheck = true;
    protected int defaultCapacity = 10;
    protected int maxSize = 10000;

    /// <summary>
    /// 保存实体管理器、玩家、预制体与池容量等基础参数，供子类创建对象池时复用。
    /// </summary>
    public virtual void Init(EntityManager entityManager, Character playerCharacter, GameObject prefab, bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 10000)
    {
        this.entityManager = entityManager;
        this.playerCharacter = playerCharacter;
        this.prefab = prefab;
        this.collectionCheck = collectionCheck;
        this.defaultCapacity = defaultCapacity;
        this.maxSize = maxSize;
    }
}
