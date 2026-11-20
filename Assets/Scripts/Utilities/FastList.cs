using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class FastList<T> : IEnumerable<T>
{
    private List<T> itemList;
    Dictionary<T, int> indexByItem;

    public int Count { get => itemList.Count; }

    /// <summary>
    /// 初始化内部数组。
    /// </summary>
    public FastList()
    {
        itemList = new List<T>();
        indexByItem = new Dictionary<T, int>();
    }

    /// <summary>
    /// 添加元素，容量不足时自动扩容。
    /// </summary>
    public void Add(T item)
    {
        if (indexByItem.ContainsKey(item))
        {
            Debug.LogError("Item already exists in list.");
            return;
        }

        int index = itemList.Count;
        itemList.Add(item);

        indexByItem[item] = index;
    }

    /// <summary>
    /// 移除元素（用末尾元素顶替，不保证顺序）。
    /// </summary>
    public void Remove(T item)
    {
        int index;
        if (!indexByItem.TryGetValue(item, out index))
        {
            Debug.LogError("Item not found in list.");
            return;
        }
        
        indexByItem.Remove(item);

        int last = itemList.Count - 1;
        // 特殊情况：删除的正好是最后一个元素
        if (index == last)
        {
            itemList.RemoveAt(last);
        }
        else
        {
            T lastItem = itemList[last];
            itemList[index] = lastItem;
            itemList.RemoveAt(last);

            indexByItem[lastItem] = index;
        }
    }

    /// <summary>
    /// 判断列表是否包含指定元素。
    /// </summary>
    public bool Contains(T item)
    {
        return indexByItem.ContainsKey(item);
    }

    /// <summary>
    /// 枚举列表内的有效元素。
    /// </summary>
    public IEnumerator<T> GetEnumerator()
    {
        foreach (T item in itemList)
        {
            yield return item;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
