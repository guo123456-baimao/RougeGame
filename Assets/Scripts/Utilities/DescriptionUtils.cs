using UnityEngine;
public class DescriptionUtils
{
    /// <summary>
    /// 生成「名称 + 数值」的升级描述文案。
    /// </summary>
    public static string GetUpgradeDescription(string upgradeName, float upgrade)
    {
        string plus = upgrade > 0 ? "+" : "";
        return upgradeName + ": " + plus + (upgrade*100).ToString() + "%\n";
    }

    /// <summary>
    /// 生成「名称 + 整数」的升级描述文案。
    /// </summary>
    public static string GetUpgradeDescription(string upgradeName, int upgrade)
    {
        string plus = upgrade > 0 ? "+" : "";
        return upgradeName + ": " + plus + upgrade.ToString() + "\n";
    }
}
