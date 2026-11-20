using UnityEngine;
[System.Serializable]
public class LootTable<T>
{
    public Loot<T>[] lootTable;

    /// <summary>
    /// 适用于掉落概率总和不足 100% 的掉落表（即物品并非必定掉落）。
    /// </summary>
    public bool TryDropLoot(out T loot)
    {
        if (TryDropLootObject(out Loot<T> lootObject))
        {
            loot = lootObject.item;
            return true;
        }
        else
        {
            loot = default(T);
            return false;
        }
    }

    /// <summary>
    /// 适用于掉落概率总和不足 100% 的掉落表（即物品并非必定掉落）。
    /// </summary>
    public bool TryDropLootObject(out Loot<T> loot)
    {
        float rand = Random.Range(0f, 1.0f);
        float cumulative = 0;
        foreach (Loot<T> drop in lootTable)
        {
            cumulative += drop.dropChance;
            if (rand < cumulative)
            {
                loot = drop;
                return true;
            }
        }
        loot = null;
        return false;
    }

    /// <summary>
    /// 适用于掉落概率总和为 100% 的掉落表（即物品必定掉落）。
    /// </summary>
    public T DropLoot()
    {
        return DropLootObject().item;
    }

    /// <summary>
    /// 适用于掉落概率总和为 100% 的掉落表（即物品必定掉落）。
    /// </summary>
    public Loot<T> DropLootObject()
    {
        float rand = Random.Range(0f, 1.0f);
        float cumulative = 0;
        foreach (Loot<T> drop in lootTable)
        {
            cumulative += drop.dropChance;
            if (rand < cumulative)
            {
                return drop;
            }
        }
        // 兜底处理：防止浮点精度误差或掉落表配置错误导致没抽到任何物品
        if (lootTable.Length > 0)
        {
            Debug.LogError("Failed to drop loot, ensure drop chances add up to 100%.");
            return lootTable[lootTable.Length-1];
        }
        Debug.LogError("Failed to drop loot, loot table is empty.");
        return null;
    }
}
