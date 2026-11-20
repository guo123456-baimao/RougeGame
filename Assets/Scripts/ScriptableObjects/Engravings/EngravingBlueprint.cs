using UnityEngine;
[CreateAssetMenu(fileName = "Engraving", menuName = "Blueprints/Engraving")]
public class EngravingBlueprint : ScriptableObject
{
    public string engravingId;
    public string displayName;
    public string description;
    public string curseDescription;
    public Sprite icon;
    public int unlockCost;

    // 正向效果
    public float hpMultiplier = 1f;
    public float damageMultiplier = 1f;
    public float speedMultiplier = 1f;
    public float lifestealChance = 0f;
    public float lifestealAmount = 0f;
    public float armorValue = 0f;
    public float luckValue = 0f;

    // 诅咒（反向效果）
    public float curseHpMultiplier = 1f;
    public float curseDamageMultiplier = 1f;
    public float curseSpeedMultiplier = 1f;
    public float curseArmorValue = 0f;

    /// <summary>
    /// 判断该铭文是否含有正面效果。
    /// </summary>
    public bool HasPositiveEffect()
    {
        return hpMultiplier != 1f || damageMultiplier != 1f ||
               speedMultiplier != 1f || lifestealChance > 0f ||
               lifestealAmount > 0f || armorValue != 0f || luckValue != 0f;
    }

    /// <summary>
    /// 判断该铭文是否带有诅咒（负面）效果。
    /// </summary>
    public bool HasCurse()
    {
        return !string.IsNullOrEmpty(curseDescription);
    }
}
