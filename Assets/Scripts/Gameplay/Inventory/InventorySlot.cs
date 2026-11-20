using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class InventorySlot : MonoBehaviour
{
    [field: SerializeField] public CollectableType CollectableType;
    [SerializeField] private GameObject countObject;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private Image itemImage;
    private List<Collectable> items;
    private FastList<Collectable> itemsBeingAdded;

    /// <summary>
    /// 初始化槽位的界面元素与空状态。
    /// </summary>
    public void Init()
    {
        items = new List<Collectable>();
        itemsBeingAdded = new FastList<Collectable>();
        countObject.SetActive(false);
        itemImage.color = new Color(1, 1, 1, 0.5f);
    }

    /// <summary>
    /// 标记该槽位已被正在飞来的物品占用。
    /// </summary>
    public void AddItemBeingCollected(Collectable item)
    {
        itemsBeingAdded.Add(item);
    }

    /// <summary>
    /// 物品到达后正式放入槽位并刷新图标。
    /// </summary>
    public void FinalizeAddItemBeingCollected(Collectable item)
    {
        if (itemsBeingAdded.Contains(item))
            itemsBeingAdded.Remove(item);
        AddItem(item);
    }

    /// <summary>
    /// 把物品放入该槽位并刷新显示。
    /// </summary>
    public void AddItem(Collectable item)
    {
        items.Add(item);
        countText.text = items.Count.ToString();
        if (!countObject.activeInHierarchy)
        {
            countObject.SetActive(true);
            itemImage.color = Color.white;
        }
    }

    /// <summary>
    /// 使用槽位内的物品并清空槽位。
    /// </summary>
    public void UseItem()
    {
        if (items.Count > 0)
        {
            AudioManager.Instance.PlayClick();
            items[0].Use();
            items.RemoveAt(0);
            countText.text = items.Count.ToString();

            if (items.Count == 0)
            {
                countObject.SetActive(false);
                itemImage.color = new Color(1, 1, 1, 0.5f);
            }
        }
    }

    /// <summary>
    /// 判断该槽位是否已被占用。
    /// </summary>
    public bool IsFull()
    {
        return items.Count + itemsBeingAdded.Count >= CollectableType.inventoryStackSize;
    }
}
