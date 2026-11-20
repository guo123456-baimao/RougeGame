using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
public abstract class Ability : MonoBehaviour
{
    [Header("Ability Details")]
    [SerializeField] protected Sprite image;
    [SerializeField] protected string localizedName;
    [SerializeField] protected string localizedDescription;
    [SerializeField] protected Rarity rarity = Rarity.Common;

    protected AbilityManager abilityManager;
    protected EntityManager entityManager;
    protected Character playerCharacter;
    protected List<IUpgradeableValue> upgradeableValues;
    protected int level = 0;
    protected int maxLevel;
    protected bool owned = false;
    public int Level { get => level; }
    public bool Owned { get => owned; }
    public Sprite Image { get => image; }
    public string Name { get => localizedName; }
    public float DropWeight { get => (float)rarity; }

    /// <summary>该技能是否有冷却周期。没有冷却的技能（如光环类）不显示冷却遮罩。</summary>
    public virtual bool HasCooldown { get => false; }

    /// <summary>冷却进度 0（刚用掉）→ 1（就绪）。只有 <see cref="HasCooldown"/> 为真时才有意义。</summary>
    public virtual float CooldownProgress { get => 1f; }
    public virtual string Description 
    { 
        get { 
            if (!owned)
                return localizedDescription;
            else
                return GetUpgradeDescriptions();
        } 
    }

    /// <summary>
    /// 注入管理器与玩家，并收集自身挂载的可升级数值。
    /// </summary>
    public virtual void Init(AbilityManager abilityManager, EntityManager entityManager, Character playerCharacter)
    {
        this.abilityManager = abilityManager;
        this.entityManager = entityManager;
        this.playerCharacter = playerCharacter;
        // 注册挂在本对象上的所有可升级字段
        upgradeableValues = this.GetType()
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
            .Where(fi => typeof(IUpgradeableValue).IsAssignableFrom(fi.FieldType))
            .Select(fi => fi.GetValue(this) as IUpgradeableValue)
            .ToList();
        upgradeableValues.ForEach(x => abilityManager.RegisterUpgradeableValue(x));
        if (upgradeableValues.Count > 0)
            maxLevel = upgradeableValues.Max(x => x.UpgradeCount) + 1;  // 最大等级 = 可升级次数总和 + 1（初始等级）
    }
    
    /// <summary>
    /// 玩家选中该技能：标记已拥有、初始化并播放选中音效。
    /// </summary>
    public virtual void Select()
    {
        if (!owned)
        {
            owned = true;
            Use();
        }
        else
        {
            Upgrade(); 
        }
        level++;
        abilityManager?.NotifyAbilitiesChanged();
    }

    /// <summary>
    /// 技能使用入口，默认无行为，由子类重写。
    /// </summary>
    protected virtual void Use()
    {
        upgradeableValues.ForEach(x => x.RegisterInUse());
    }

    /// <summary>
    /// 技能升级入口，默认无行为，由子类重写。
    /// </summary>
    protected virtual void Upgrade()
    {
        upgradeableValues.ForEach(x => x.Upgrade());
    }

    /// <summary>
    /// 在屏幕范围内寻找最近敌人并返回指向它的单位向量（没有敌人时返回随机方向）。
    /// </summary>
    protected Vector2 FindNearestEnemyDirection()
    {
        var nearby = entityManager.Grid.FindNearbyInRadius(
            (Vector2)playerCharacter.transform.position, 15f);
        if (nearby.Count == 0) return playerCharacter.LookDirection;
        Vector2 closestPos = nearby[0].Position;
        float minDist = Vector2.Distance(closestPos, (Vector2)playerCharacter.transform.position);
        foreach (var client in nearby)
        {
            float d = Vector2.Distance(client.Position, (Vector2)playerCharacter.transform.position);
            if (d < minDist)
            {
                minDist = d;
                closestPos = client.Position;
            }
        }
        return (closestPos - (Vector2)playerCharacter.transform.position).normalized;
    }

    /// <summary>
    /// 判断该技能当前是否满足出现条件，默认总是满足。
    /// </summary>
    public virtual bool RequirementsMet()
    {
        return level < maxLevel;
    }

    /// <summary>
    /// 拼接所有可升级数值的描述，用于卡片文案。
    /// </summary>
    protected string GetUpgradeDescriptions()
    {
        string description = "";
        upgradeableValues.ForEach(x => description += x.GetUpgradeDescription());
        return description;
    }

    public enum Rarity
    {
        Common = 50,
        Uncommon = 25,
        Rare = 15,
        Legendary = 9,
        Exotic = 1
    }
}
