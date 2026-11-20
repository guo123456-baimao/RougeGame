using UnityEngine;
using UnityEngine.Serialization;
[CreateAssetMenu(fileName = "Character", menuName = "Blueprints/Character", order = 1)]
public class CharacterBlueprint : ScriptableObject
{
    public new string name;
    [SerializeField] public string uid;
    [SerializeField] public int cost = 999;

    /// <summary>
    /// 「默认赠送」配置：勾上的角色开局即拥有（主角色 Blue 就是这么来的）。
    ///
    /// 这是静态配置，运行时只读。旧版把「当前是否拥有」存在这里，游戏一跑就写回
    /// 资产文件 —— 于是 .asset 永远脏着。现在拥有状态的唯一数据源是 IDataStore，
    /// 这里只负责回答「这个角色是不是白送的」。
    /// </summary>
    [FormerlySerializedAs("owned")]
    [SerializeField] private bool ownedByDefault = false;

    /// <summary>当前是否已解锁。查 IDataStore，默认赠送的角色额外兜底。</summary>
    public bool IsOwned => ownedByDefault || DataStoreLocator.Store.IsCharacterOwned(uid);

    public virtual int GetCost() => cost;
    public float hp;
    public float recovery;
    public int armor;
    public float movespeed;
    public float luck;
    public float acceleration;
    public Sprite[] walkSpriteSequence;
    public float walkFrameTime;
    public GameObject[] startingAbilities;
    // 铭文统一走全局库 Resources/EngravingDatabase.asset，角色不再各自挂一份

    /// <summary>
    /// 返回从该等级升到下一级所需的经验增量。
    /// </summary>
    public float LevelToExpIncrease(int level)
    {
        if (level < 10)
            return 10;
        if (level < 20)
            return 13;
        if (level < 30)
            return 16;
        else
            return 20;
    }

    // 拥有状态统一走 IDataStore（键：CharOwned_{uid}）。
    // 这里不再提供 Save/LoadOwnership —— 那两个方法会写回资产文件，让 .asset 变脏。
}
