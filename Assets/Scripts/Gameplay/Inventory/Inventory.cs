using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class Inventory : MonoBehaviour
{
    [SerializeField] private InventorySlot[] inventorySlots;
    private Dictionary<CollectableType, InventorySlot> inventorySlotByType;

    /// <summary>
    /// 缓存并初始化所有背包槽位。
    /// </summary>
    public void Init()
    {
        inventorySlotByType = new Dictionary<CollectableType, InventorySlot>();
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            InventorySlot inventorySlot = inventorySlots[i];
            inventorySlot.Init();
            inventorySlotByType[inventorySlot.CollectableType] = inventorySlot;
        }
    }

    /// <summary>
    /// 判断对应类型的槽位是否还有空位。
    /// </summary>
    public bool RoomInInventory(Collectable item)
    {
        if (inventorySlotByType.TryGetValue(item.CollectableType, out InventorySlot inventorySlot))
            return !inventorySlot.IsFull();
        return false;
    }

    /// <summary>
    /// 按物品类型取出对应的背包槽位。
    /// </summary>
    public bool TryGetInventorySlot(Collectable item, out InventorySlot inventorySlot)
    {
        if (inventorySlotByType.TryGetValue(item.CollectableType, out inventorySlot))
        {
            return true;
        }
        inventorySlot = null;
        return false;
    }

    /// <summary>
    /// 把物品放入对应类型的空槽位，失败时返回 null。
    /// </summary>
    public InventorySlot AddItem(Collectable item)
    {
        if (inventorySlotByType.ContainsKey(item.CollectableType) && !inventorySlotByType[item.CollectableType].IsFull())
        {
            inventorySlotByType[item.CollectableType].AddItem(item);
            return inventorySlotByType[item.CollectableType];
        }
        return null;
    }
}
