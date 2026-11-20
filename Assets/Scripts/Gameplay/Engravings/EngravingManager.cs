using System;
using System.Collections.Generic;
using UnityEngine;
public static class EngravingManager
{
    public static readonly string UnlockedKey = "Engravings_Unlocked";
    public static readonly string SelectedKey = "Engravings_Selected";
    public static readonly int MaxSelectedSlots = 3;
    public static event System.Action OnSelectedChanged;

    /// <summary>
    /// 铭文提供的全局伤害倍率（含诅咒），由 Character 每局结算时写入，
    /// UpgradeableDamage / UpgradeableBleedDamage 读取伤害时统一乘上。
    /// </summary>
    public static float DamageMultiplier { get; set; } = 1f;

    private static EngravingDatabase database;
    private static readonly EngravingBlueprint[] EmptyPool = new EngravingBlueprint[0];

    /// <summary>
    /// 由场景在角色初始化之前注入（Game 场景的 LevelManager）。
    /// 必须在 Character.Init 之前调用，否则本局的铭文结算拿不到库。
    /// </summary>
    public static void SetDatabase(EngravingDatabase engravingDatabase)
    {
        database = engravingDatabase;
        if (database == null)
        {
            Debug.LogError("[EngravingManager] 铭文库未注入！请在场景的 LevelManager 上把 Engraving Database 字段指定为 " +
                           "Assets/Blueprints/Engravings/EngravingDatabase.asset，否则铭文不会生效。");
        }
    }

    /// <summary>全局铭文库，UI 与角色结算共用同一份数据。</summary>
    public static EngravingBlueprint[] AllEngravings
    {
        get
        {
            return database != null && database.allEngravings != null
                ? database.allEngravings
                : EmptyPool;
        }
    }

    /// <summary>
    /// 广播已装备铭文发生变化的事件。
    /// </summary>
    private static void NotifySelectedChanged()
    {
        var handler = OnSelectedChanged;
        if (handler != null)
            handler.Invoke();
    }

    public static List<string> UnlockedEngravings
    {
        get { return new List<string>(DataStoreLocator.Store.GetUnlockedEngravings()); }
    }

    public static List<string> SelectedEngravings
    {
        get { return new List<string>(DataStoreLocator.Store.GetSelectedEngravings()); }
    }

    /// <summary>
    /// 判断指定铭文是否已解锁。
    /// </summary>
    public static bool IsUnlocked(string engravingId)
    {
        return UnlockedEngravings.Contains(engravingId);
    }

    /// <summary>
    /// 解锁铭文。扣费 + 写入已解锁列表由服务端（或本地存储）按同一份订单完成，
    /// 所以结果通过回调返回，onResult(true) 才代表真的解锁了。
    /// </summary>
    public static void Unlock(EngravingBlueprint engraving, Action<bool> onResult)
    {
        if (engraving == null || IsUnlocked(engraving.engravingId))
        {
            onResult?.Invoke(false);
            return;
        }

        var store = DataStoreLocator.Store;
        if (store.GetCoins() < engraving.unlockCost)
        {
            onResult?.Invoke(false);
            return;
        }

        store.SpendCoins(SpendOrder.ForEngraving(engraving.engravingId, engraving.unlockCost), outcome =>
        {
            if (!outcome.Success)
            {
                Debug.LogWarning($"[EngravingManager] 解锁失败：{outcome.Message}");
                onResult?.Invoke(false);
                return;
            }

            onResult?.Invoke(true);
        });
    }

    /// <summary>
    /// 切换铭文的装备状态：已装备则卸下，未装备且槽位未满时装备。
    /// </summary>
    public static void Select(EngravingBlueprint engraving)
    {
        var selected = SelectedEngravings;
        if (selected.Contains(engraving.engravingId))
        {
            selected.Remove(engraving.engravingId);
        }
        else if (selected.Count < MaxSelectedSlots)
        {
            selected.Add(engraving.engravingId);
        }
        DataStoreLocator.Store.SetSelectedEngravings(selected);
        NotifySelectedChanged();
    }

    /// <summary>
    /// 清空所有已装备的铭文。
    /// </summary>
    public static void ClearSelected()
    {
        DataStoreLocator.Store.ClearSelectedEngravings();
        NotifySelectedChanged();
    }

    /// <summary>
    /// 按 id 在全局铭文池中查找铭文。
    /// </summary>
    public static EngravingBlueprint GetEngraving(string id)
    {
        return GetEngraving(id, AllEngravings);
    }

    /// <summary>
    /// 在指定铭文池中按 id 查找铭文。
    /// </summary>
    public static EngravingBlueprint GetEngraving(string id, IList<EngravingBlueprint> pool)
    {
        if (pool == null) return null;
        foreach (var e in pool)
        {
            if (e != null && e.engravingId == id) return e;
        }
        return null;
    }
}
