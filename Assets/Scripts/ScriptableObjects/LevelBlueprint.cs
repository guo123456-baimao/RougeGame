using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
[CreateAssetMenu(fileName = "Level", menuName = "Blueprints/Level", order = 1)]
public class LevelBlueprint : ScriptableObject
{
    [Header("Time")]
    public float levelTime = 600;
    [Header("Background")]
    public Texture2D backgroundTexture;
    [Header("Abilities")]
    public GameObject[] abilityPrefabs;
    [Header("Monster Settings")]
    public MonstersContainer[] monsters;
    public MiniBossContainer[] miniBosses;
    public BossContainer finalBoss;
    public MonsterSpawnTable monsterSpawnTable;
    [Header("Phases 三段式关卡（可选）")]
    [Tooltip("相位列表。每段 duration 之和必须等于 levelTime 且每段 spawnTable 齐全，否则回退单表刷怪逻辑并打印 Warning")]
    public LevelPhase[] phases;
    [Header("Elite Settings")]
    [Tooltip("每只普通怪的精英化概率，0 表示关闭精英（走原逻辑）")]
    [Range(0f, 1f)] public float eliteChance = 0.03f;
    [Tooltip("连续 N 只普通怪后必出 1 只精英，<= 0 关闭保底")]
    public int elitePityCount = 40;
    [Tooltip("场上同时存在的精英上限")]
    public int maxEliteAlive = 8;
    [Tooltip("精英最终血量倍率（在 Setup 后的 maxHealth 上乘一次）")]
    public float eliteHpMultiplier = 4f;
    [Tooltip("精英伤害倍率（接触 / 弹幕 / 爆炸）")]
    public float eliteDamageMultiplier = 1.5f;
    [Tooltip("精英攻速倍率，1 表示不变")]
    public float eliteAttackSpeedMultiplier = 1f;
    [Tooltip("精英移速倍率")]
    public float eliteSpeedMultiplier = 1.1f;
    [Tooltip("精英体型倍率，同时放大碰撞体")]
    public float eliteScale = 1.3f;
    [Tooltip("精英死亡保底掉落的宝箱数量")]
    public int eliteChestDrop = 1;
    [Tooltip("精英染色，用于一眼分辨")]
    public Color eliteTint = new Color(1f, 0.66f, 0.22f, 1f);
    [Header("Chest Settings")]
    public ChestBlueprint chestBlueprint;
    public float chestSpawnDelay = 30;
    public float chestSpawnAmount = 2;
    [Header("Exp Gem Settings")]
    public int initialExpGemCount = 25;
    public GemType initialExpGemType = GemType.White1;

    private Dictionary<int, (int, int)> monsterIndexMap;
    public Dictionary<int, (int, int)> MonsterIndexMap 
    { 
        get
        {
            if (monsterIndexMap == null)
            {
                monsterIndexMap = new Dictionary<int, (int, int)>();
                int monsterIndex = 0;
                for (int i = 0; i < monsters.Length; i++)
                {
                    for (int j = 0; j < monsters[i].monsterBlueprints.Length; j++)
                        monsterIndexMap[monsterIndex++] = (i, j);
                }
            }
            return monsterIndexMap;
        }
    }

    [System.Serializable]
    public class MonstersContainer
    {
        [Tooltip("该容器共用的怪物预制体（可寻址引用，开局由 ResManager 按引用计数加载）")]
        public AssetReferenceGameObject monstersPrefab;
        public MonsterBlueprint[] monsterBlueprints;
    }

    [System.Serializable]
    public class MiniBossContainer : BossContainer
    {
        public float spawnTime;
    }

    [System.Serializable]
    public class BossContainer
    {
        [Tooltip("Boss 预制体（可寻址引用，开局由 ResManager 按引用计数加载）")]
        public AssetReferenceGameObject bossPrefab;
        public BossMonsterBlueprint bossBlueprint;
    }

    [System.Serializable]
    public class LevelPhase
    {
        [Tooltip("相位显示名，例如「矿区」，会显示在 HUD 进度条上")]
        public string displayName;
        [Tooltip("HUD 相位名强调色（矿区暖黄 / 血色地暗红 / 虚空幽紫），相位切换时渐变过渡")]
        public Color labelTint = Color.white;
        [Tooltip("该相位持续秒数，所有相位之和必须等于 levelTime")]
        public float duration = 100;
        [Tooltip("该相位背景贴图，留空则沿用当前背景不切换")]
        public Texture2D background;
        [Tooltip("该相位独立刷怪表，t 在相位内重新归一化（开场稀疏、结尾密集）")]
        public MonsterSpawnTable spawnTable;
        [Tooltip("该相位小 Boss 配置。复用 finalBoss.bossPrefab 的 Boss 池，仅蓝图可换，不新增池")]
        public BossContainer phaseMiniBoss;
        [Tooltip("小 Boss 刷新时间，相对该相位开始的秒数")]
        public float miniBossSpawnTime = 100;
    }

    /// <summary>
    /// 校验 phases 配置是否可用：非空、每段 duration > 0 且 spawnTable 齐全、
    /// 时长总和与 levelTime 一致（0.5s 容差）。校验通过时输出各相位起始时间。
    /// 不满足任意条件则返回 false，调用方应回退到单表刷怪逻辑。
    /// </summary>
    public bool TryGetValidPhases(out float[] phaseStartTimes)
    {
        phaseStartTimes = null;
        if (phases == null || phases.Length == 0)
            return false;
        float[] starts = new float[phases.Length];
        float total = 0;
        for (int i = 0; i < phases.Length; i++)
        {
            if (phases[i] == null || phases[i].duration <= 0 || phases[i].spawnTable == null)
                return false;
            starts[i] = total;
            total += phases[i].duration;
        }
        if (Mathf.Abs(total - levelTime) > 0.5f)
            return false;
        phaseStartTimes = starts;
        return true;
    }
}
