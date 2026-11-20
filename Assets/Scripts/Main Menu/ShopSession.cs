using System.Collections.Generic;
using System.Text;
using UnityEngine;
/// <summary>购买被拒绝的原因。</summary>
public enum ShopDenyReason
{
    None,
    AlreadyPurchased,
    LimitReached,
    NotEnoughCoins
}

/// <summary>
/// 单局商店购买记录：同一局内同一件商品不能重复购买（避免属性重复叠加），
/// 且每局开始前最多买 MaxPurchasesPerRound 件。
/// 一局结束（死亡或通关）时由 Character.ClearSessionData 清空。
/// </summary>
public static class ShopSession
{
    public const int MaxPurchasesPerRound = 3;

    public static int PurchasedCount => GetPurchasedNames().Count;
    public static int Remaining => Mathf.Max(0, MaxPurchasesPerRound - PurchasedCount);

    /// <summary>单局购买记录直接委托给 RunState（内存态，局末随 RunState.Clear 一起清）。</summary>
    public static List<string> GetPurchasedNames()
    {
        return RunState.SessionPurchasedItems;
    }

    /// <summary>
    /// 判断该商品在当前这一局里是否已经买过。
    /// </summary>
    public static bool IsPurchased(ShopItemBlueprint item)
    {
        return item != null && RunState.SessionPurchasedItems.Contains(item.name);
    }

    /// <summary>
    /// 购买成功后才调用；只记录名字，属性由各 ShopItemBlueprint.Purchase 自己结算。
    /// 记完立刻落盘到当前账号：金币是从账号扣的，买到的东西也必须写在账号上，
    /// 否则退出登录再回来就变成"花了钱什么都没有"。
    /// </summary>
    public static void Record(ShopItemBlueprint item)
    {
        if (item == null || IsPurchased(item))
            return;

        RunState.SessionPurchasedItems.Add(item.name);
        Save();
    }

    /// <summary>
    /// 一局结束（死亡 / 通关 / 返回首页）：清内存并同步清空账号里的本局记录，
    /// 否则下次登录会把上一局的残留又捞回来。
    /// </summary>
    public static void Clear()
    {
        RunState.Clear();
        Save();
    }

    /// <summary>把当前本局进度写进当前身份的存档（联网写服务器，离线写本地）。</summary>
    public static void Save()
    {
        DataStoreLocator.Store.SetSessionSnapshot(RunState.Serialize());
    }

    /// <summary>
    /// 切换身份（退出登录 / 登录 / 会话恢复）后调用：
    /// 把新身份存档里的本局进度读回内存。上一个身份的留在它自己的存档里，两边互不干扰。
    /// </summary>
    public static void LoadFromStore()
    {
        RunState.Restore(DataStoreLocator.Store.GetSessionSnapshot());
    }

    /// <summary>
    /// 判定这次购买被拒绝的原因，可以购买时返回无需拦截。
    /// </summary>
    public static ShopDenyReason CheckPurchase(ShopItemBlueprint item)
    {
        if (item == null || IsPurchased(item))
            return ShopDenyReason.AlreadyPurchased;
        if (PurchasedCount >= MaxPurchasesPerRound)
            return ShopDenyReason.LimitReached;
        if (DataStoreLocator.Store.GetCoins() < item.GetCost())
            return ShopDenyReason.NotEnoughCoins;
        return ShopDenyReason.None;
    }

    /// <summary>本局已购商品的属性总加成，例如「生命 +10 · 护甲 +10 · 速度 +1%」。</summary>
    public static string GetBonusText(IList<ShopItemBlueprint> allItems)
    {
        if (allItems == null)
            return string.Empty;

        int hp = 0, armor = 0, speed = 0, luck = 0;
        var others = new List<string>();

        foreach (var item in allItems)
        {
            if (item == null || !IsPurchased(item))
                continue;

            var boost = item as ShopStatBoost;
            if (boost == null)
            {
                string display = item.GetDisplayName();
                if (!others.Contains(display))
                    others.Add(display);
                continue;
            }

            switch (boost.boostType)
            {
                case ShopStatBoost.StatBoostType.HP: hp += boost.boostAmount; break;
                case ShopStatBoost.StatBoostType.Armor: armor += boost.boostAmount; break;
                case ShopStatBoost.StatBoostType.MoveSpeed: speed += boost.boostAmount; break;
                case ShopStatBoost.StatBoostType.Luck: luck += boost.boostAmount; break;
            }
        }

        var sb = new StringBuilder();
        AppendBonus(sb, "生命", hp, string.Empty);
        AppendBonus(sb, "护甲", armor, string.Empty);
        AppendBonus(sb, "速度", speed, "%");
        AppendBonus(sb, "幸运", luck, string.Empty);
        foreach (var other in others)
        {
            if (sb.Length > 0) sb.Append("  ·  ");
            sb.Append(other);
        }
        return sb.ToString();
    }

    /// <summary>
    /// 把某项属性加成按统一格式追加到汇总文本末尾，数值为零则跳过。
    /// </summary>
    private static void AppendBonus(StringBuilder sb, string label, int value, string suffix)
    {
        if (value <= 0) return;
        if (sb.Length > 0) sb.Append("  ·  ");
        sb.Append(label).Append(" +").Append(value).Append(suffix);
    }
}
