using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class AbilityManager : MonoBehaviour
{
    private LevelBlueprint levelBlueprint;
    private Character playerCharacter;
    private WeightedAbilities newAbilities;
    private WeightedAbilities ownedAbilities;
    private FastList<IUpgradeableValue> registeredUpgradeableValues;
    public int DamageUpgradeablesCount { get; set; } = 0;
    public int KnockbackUpgradeablesCount { get; set; } = 0;
    public int WeaponCooldownUpgradeablesCount { get; set; } = 0;
    public int RecoveryCooldownUpgradeablesCount { get; set; } = 0;
    public int AOEUpgradeablesCount { get; set; } = 0;
    public int ProjectileSpeedUpgradeablesCount { get; set; } = 0;
    public int ProjectileCountUpgradeablesCount { get; set; } = 0;
    public int RecoveryUpgradeablesCount { get; set; } = 0;
    public int RecoveryChanceUpgradeablesCount { get; set; } = 0;
    public int BleedDamageUpgradeablesCount { get; set; } = 0;
    public int BleedRateUpgradeablesCount { get; set; } = 0;
    public int BleedDurationUpgradeablesCount { get; set; } = 0;
    public int MovementSpeedUpgradeablesCount { get; set; } = 0;
    public int ArmorUpgradeablesCount { get; set; } = 0;
    public int FireRateUpgradeablesCount { get; set; } = 0;
    public int DurationUpgradeablesCount { get; set; } = 0;
    public int RotationSpeedUpgradeablesCount { get; set; } = 0;

    /// <summary>已拥有技能发生变化（获得新技能 / 升级 / 全部销毁）时触发，HUD 据此刷新技能栏。</summary>
    public event System.Action OnAbilitiesChanged;

    /// <summary>玩家当前已拥有的技能（不含未获取的候选池）。</summary>
    public IEnumerable<Ability> OwnedAbilities => ownedAbilities;

    /// <summary>
    /// 广播技能列表变化，HUD 技能栏据此刷新。
    /// </summary>
    public void NotifyAbilitiesChanged()
    {
        OnAbilitiesChanged?.Invoke();
    }

    /// <summary>
    /// 按关卡蓝图重建可抽技能池，并注册角色自带的初始技能。
    /// </summary>
    public void Init(LevelBlueprint levelBlueprint, EntityManager entityManager, Character playerCharacter, AbilityManager abilityManager)
    {
        this.levelBlueprint = levelBlueprint;
        this.playerCharacter = playerCharacter;

        registeredUpgradeableValues = new FastList<IUpgradeableValue>();

        ownedAbilities = new WeightedAbilities();
        foreach (GameObject abilityPrefab in playerCharacter.Blueprint.startingAbilities)
        {
            Ability ability = Instantiate(abilityPrefab, transform).GetComponent<Ability>();
            ability.Init(abilityManager, entityManager, playerCharacter);
            ability.Select();
            ownedAbilities.Add(ability);
        }
        
        newAbilities = new WeightedAbilities();
        foreach (GameObject abilityPrefab in levelBlueprint.abilityPrefabs)
        {
            // 跳过已经拥有的技能
            if (playerCharacter.Blueprint.startingAbilities.Contains(abilityPrefab)) continue;
            
            Ability ability = Instantiate(abilityPrefab, transform).GetComponent<Ability>();
            ability.Init(abilityManager, entityManager, playerCharacter);
            newAbilities.Add(ability);
        }

        NotifyAbilitiesChanged();
    }

    /// <summary>
    /// 把可升级数值登记到技能系统中，可选地标记为已使用。
    /// </summary>
    public void RegisterUpgradeableValue(IUpgradeableValue upgradeableValue, bool inUse = false)
    {
        upgradeableValue.Register(this);
        registeredUpgradeableValues.Add(upgradeableValue);
        if (inUse) upgradeableValue.RegisterInUse();
    }

    public void UpgradeValue<T, TValue>(TValue value) where T : IUpgradeableValue
    {
        UpgradeableValue<TValue>[] upgradeableValues = registeredUpgradeableValues.OfType<T>().ToArray() as UpgradeableValue<TValue>[];
        foreach (UpgradeableValue<TValue> upgradeableValue in upgradeableValues)
        {
            upgradeableValue.Upgrade(value);
        }
    }

    /// <summary>
    /// 抽取本次可供选择的技能。
    /// </summary>
    public List<Ability> SelectAbilities()
    {
        List<Ability> selectedAbilities = new List<Ability>();
        
        // 统计当前可用的技能（即满足解锁条件的技能）
        WeightedAbilities availableOwnedAbilities = ExtractAvailableAbilities(ownedAbilities);
        WeightedAbilities availableNewAbilities = ExtractAvailableAbilities(newAbilities);

        // 决定本次总共抽几张卡（3 ~ 4 张）
        int selectedAbilitiesCount = 3 + (ResolveChance(FourthChance) ? 1 : 0);

        // 先尽量给玩家展示最多 2 个已拥有的技能（用于升级）
        int ownedAbilitiesCount = availableOwnedAbilities.Count < 2 ? availableOwnedAbilities.Count : 2;
        for (int i = 0; i < ownedAbilitiesCount; i++)
        {
            if (ResolveChance(OwnedChance))
                selectedAbilities.Add(PullAbility(availableOwnedAbilities));
        }

        // 从可用技能池中抽取剩余的卡
        int availableAbilitiesCount = selectedAbilitiesCount - selectedAbilities.Count;
        if (availableAbilitiesCount > availableNewAbilities.Count) availableAbilitiesCount = availableNewAbilities.Count;
        for (int i = 0; i < availableAbilitiesCount; i++)
        {
            selectedAbilities.Add(PullAbility(availableNewAbilities));
        }

        // 若还有空位，尽量用已拥有的技能补齐
        for (int i = selectedAbilities.Count; i < selectedAbilitiesCount && i - selectedAbilities.Count < availableOwnedAbilities.Count; i++)
        {
            selectedAbilities.Add(PullAbility(availableOwnedAbilities));
        }

        // 把抽取过但未被选中的可用技能放回对应池子
        foreach (Ability ability in availableNewAbilities)
            newAbilities.Add(ability);
        foreach (Ability ability in availableOwnedAbilities)
            ownedAbilities.Add(ability);

        return selectedAbilities;   
    }
    
    /// <summary>
    /// 把选卡期间临时抽出的技能还回池子。
    /// 必须在这里广播：Ability.Select() 触发的那次刷新，此刻技能还不在 ownedAbilities 里
    /// （被 SelectAbilities 抽走了），HUD 看不到它，新技能图标就不会出现。
    /// </summary>
    public void ReturnAbilities(List<Ability> abilities)
    {
        if (abilities == null) return;

        foreach (Ability ability in abilities)
        {
            if (ability == null) continue;

            if (ability.Owned)
            {
                ownedAbilities.Add(ability);
            }
            else
            {
                newAbilities.Add(ability);
            }
        }

        NotifyAbilitiesChanged();
    }

    /// <summary>
    /// 销毁所有已激活的技能实例（死亡或结束一局时调用）。
    /// </summary>
    public void DestroyActiveAbilities()
    {
        foreach (Ability ability in ownedAbilities)
        {
            Destroy(ability.gameObject);
        }
        ownedAbilities = new WeightedAbilities();
        NotifyAbilitiesChanged();
    }

    /// <summary>
    /// 判断是否还有可选技能，用于无技能可选时的兜底处理。
    /// </summary>
    public bool HasAvailableAbilities()
    {
        foreach (Ability ability in ownedAbilities)
        {
            if (ability.RequirementsMet())
                return true;
        }

        foreach (Ability ability in newAbilities)
        {
            if (ability.RequirementsMet())
                return true;
        }

        return false;
    }

    /// <summary>
    /// 筛出满足条件且未满级的技能，组成新的加权技能池。
    /// </summary>
    private WeightedAbilities ExtractAvailableAbilities(WeightedAbilities abilities)
    {
        WeightedAbilities availableAbilities = new WeightedAbilities();

        foreach (Ability ability in abilities)
        {
            if (ability.RequirementsMet())
                availableAbilities.Add(ability);
        }

        foreach (Ability ability in availableAbilities)
        {
            abilities.Remove(ability);
        }

        return availableAbilities;
    }

    /// <summary>
    /// 按权重从给定的技能列表中抽取一个技能。
    /// </summary>
    private Ability PullAbility(WeightedAbilities abilities)
    {
        float rand = Random.Range(0f, abilities.Weight);
        float cumulative = 0;
        foreach (Ability ability in abilities)
        {
            cumulative += ability.DropWeight;
            if (rand < cumulative)
            {
                abilities.Remove(ability);
                return ability;
            }
        }
        Debug.LogError("Failed to pull ability!");
        return null;
    }

    /// <summary>
    /// 玩家已拥有的技能出现的概率
    /// （使其可以升级）
    /// </summary>
    private float OwnedChance()
    {
        float x = playerCharacter.CurrentLevel % 2 == 0 ? 2 : 1;
        return 1 + 0.3f*x - 1/playerCharacter.Luck;
    }

    /// <summary>
    /// 出现第四个技能/升级选项的概率
    /// </summary>
    private float FourthChance()
    {
        return 1 - 1/playerCharacter.Luck;
    }

    /// <summary>
    /// 结算一个概率函数（按返回的概率随机判定是否命中）。
    /// </summary>
    private bool ResolveChance(System.Func<float> chanceFunction)
    {
        return Random.Range(0.0f, 1.0f) < chanceFunction();
    }

    private class WeightedAbilities : IEnumerable<Ability>
    {
        private FastList<Ability> abilities;
        private float weight;
        public float Weight { get => weight; set => weight = value; }
        public int Count { get => abilities.Count; }

        /// <summary>
        /// 构造一个空的加权技能池。
        /// </summary>
        public WeightedAbilities()
        {
            abilities = new FastList<Ability>();
            weight = 0;
        }

        /// <summary>
        /// 向池中添加技能并累计总权重。
        /// </summary>
        public void Add(Ability ability)
        {
            abilities.Add(ability);
            weight += ability.DropWeight;
        }

        /// <summary>
        /// 从池中移除技能并扣减总权重。
        /// </summary>
        public void Remove(Ability ability)
        {
            weight -= ability.DropWeight;
            abilities.Remove(ability);
        }

        /// <summary>
        /// 按权重从高到低枚举池内技能。
        /// </summary>
        public IEnumerator<Ability> GetEnumerator()
        {
            foreach (Ability ability in abilities)
            {
                yield return ability;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
