using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 本局进度的存档形态。
///
/// 这些数字原本只活在内存里（RunState 静态字段），一旦退出登录就被丢掉，
/// 而金币却已经从账号里扣了 —— 玩家花了钱什么都没剩下。
/// 现在把它序列化后存进账号（联网存服务器、离线存本地），局末清空。
/// </summary>
[System.Serializable]
public class RunStateSnapshot
{
    /// <summary>本局已购商品名（决定商店灰态、每局 3 件上限、能否重买）。</summary>
    public string[] items;

    /// <summary>已购买但还没发到角色身上的加成（开局 ApplySessionBoosts 消费后归零）。</summary>
    public float hp;
    public int armor;
    public float speed;
    public float luck;

    /// <summary>已购买、等进入游戏场景投放的消耗品，格式 "CollectableTypeHash:数量"。</summary>
    public string[] consumables;
}

/// <summary>
/// 单局临时数据：跨场景存活（主菜单买 → 游戏场景结算），一局结束（死亡/通关/返回首页）清空。
/// 替代原 PlayerPrefs 的 Session_* 键——这些数据本质是"跨场景传参"，不该落盘：
/// 内存静态类的生命周期恰好就是"一次游戏运行"，中途强杀游戏丢失的也只是未结算的单局加成，语义更正确。
/// </summary>
public static class RunState
{
    // ---- 单局属性加成（ShopStatBoost 购买时累加，Character.ApplySessionBoosts 消费）----
    public static float HpBoost;
    public static int ArmorBoost;
    public static float MoveSpeedBoost;
    public static float LuckBoost;

    // ---- 单局已购商品名（ShopSession 使用，控制"每局上限 3 件 / 不可重复购买"）----
    public static readonly List<string> SessionPurchasedItems = new List<string>();

    // ---- 单局购买、待下一局出生时投放的消耗品，格式 "CollectableTypeHash:数量" ----
    public static readonly List<string> PurchasedConsumableEntries = new List<string>();

    /// <summary>
    /// 把商店购买的属性加成累加到本局状态的对应字段。
    /// </summary>
    public static void AddStatBoost(ShopStatBoost.StatBoostType type, int amount)
    {
        switch (type)
        {
            case ShopStatBoost.StatBoostType.HP: HpBoost += amount; break;
            case ShopStatBoost.StatBoostType.Armor: ArmorBoost += amount; break;
            case ShopStatBoost.StatBoostType.MoveSpeed: MoveSpeedBoost += amount; break;
            case ShopStatBoost.StatBoostType.Luck: LuckBoost += amount; break;
        }
    }

    /// <summary>
    /// 把待投放消耗品按「类型名:数量」记入单局列表。
    /// </summary>
    public static void AddPurchasedConsumable(string collectableTypeName, int amount)
    {
        if (amount <= 0) return;
        // 用 CollectableType 资产名做 key：跨会话稳定；之前的运行时 hash 重启后对不上
        PurchasedConsumableEntries.Add(collectableTypeName + ":" + amount);
    }

    /// <summary>取走全部待投放消耗品条目（读后即清，模拟原 PlayerPrefs 读后 DeleteKey 的语义）。</summary>
    public static List<string> TakePurchasedConsumableEntries()
    {
        var copy = new List<string>(PurchasedConsumableEntries);
        PurchasedConsumableEntries.Clear();
        return copy;
    }

    /// <summary>一局结束：清空全部单局数据（由 Character.ClearSessionData 调用）。</summary>
    public static void Clear()
    {
        HpBoost = 0f;
        ArmorBoost = 0;
        MoveSpeedBoost = 0f;
        LuckBoost = 0f;
        SessionPurchasedItems.Clear();
        PurchasedConsumableEntries.Clear();
    }

    /// <summary>本局什么都没有时返回空串，存档里就不需要留一条空记录。</summary>
    public static bool IsEmpty()
    {
        return HpBoost == 0f && ArmorBoost == 0 && MoveSpeedBoost == 0f && LuckBoost == 0f
               && SessionPurchasedItems.Count == 0 && PurchasedConsumableEntries.Count == 0;
    }

    /// <summary>序列化成存档串，交给 IDataStore 存进账号（联网=服务器 / 离线=本地）。</summary>
    public static string Serialize()
    {
        if (IsEmpty()) return string.Empty;

        return JsonUtility.ToJson(new RunStateSnapshot
        {
            items = SessionPurchasedItems.ToArray(),
            hp = HpBoost,
            armor = ArmorBoost,
            speed = MoveSpeedBoost,
            luck = LuckBoost,
            consumables = PurchasedConsumableEntries.ToArray()
        });
    }

    /// <summary>用存档串恢复本局进度；空串或解析失败一律停在干净状态，不会留下半个本局。</summary>
    public static void Restore(string snapshot)
    {
        Clear();
        if (string.IsNullOrEmpty(snapshot)) return;

        RunStateSnapshot data;
        try { data = JsonUtility.FromJson<RunStateSnapshot>(snapshot); }
        catch { return; }
        if (data == null) return;

        if (data.items != null)
            foreach (var item in data.items)
                if (!string.IsNullOrEmpty(item) && !SessionPurchasedItems.Contains(item))
                    SessionPurchasedItems.Add(item);

        HpBoost = data.hp;
        ArmorBoost = data.armor;
        MoveSpeedBoost = data.speed;
        LuckBoost = data.luck;

        if (data.consumables != null)
            foreach (var entry in data.consumables)
                if (!string.IsNullOrEmpty(entry) && !PurchasedConsumableEntries.Contains(entry))
                    PurchasedConsumableEntries.Add(entry);
    }
}
