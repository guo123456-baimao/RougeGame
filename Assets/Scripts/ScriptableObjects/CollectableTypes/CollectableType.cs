using UnityEngine;
using UnityEngine.AddressableAssets;
public abstract class CollectableType : ScriptableObject
{
    [Tooltip("拾取物预制体（可寻址引用，进图后由 ResManager 按引用计数加载）")]
    public AssetReferenceGameObject itemPrefab;
    public int inventoryStackSize = 0;
}
