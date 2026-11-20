using UnityEngine;
/// <summary>精英词缀。同一只精英最多携带 1 个，由 enum + switch 驱动，不做字符串匹配。</summary>
public enum EliteAffix
{
    None = 0,
    Split = 1,          // 分裂：死亡时生成 3 只 30% 血量的同类（从地里升起）
    Shield = 2,         // 护盾：血条后追加一条等长白色护盾条，抵伤且每 2 秒回复 20% 最大护盾
    DeathExplosion = 3, // 死亡爆炸：预警 1 秒后，半径 1.2 内造成 攻击力 × 4
    Haste = 4,          // 疾行：移速 ×2.5，附带黑色残影
    Regeneration = 5,   // 再生：每 1 秒回复 15% 最大生命，回血时颜色向浅绿往返
    Summon = 6          // 召唤：每 5 秒召唤 1 只普通同级怪（从地里升起）
}

/// <summary>
/// 刷怪时由 LevelManager 计算，随 SpawnMonster 一路传入 Monster.Setup 的精英参数。
/// 值类型，避免每次刷怪产生托管分配；未配置时全部为 0/false，即普通怪。
/// tint 与 isElite 解耦：小怪也可以只继承精英的染色而不继承属性。
/// </summary>
public struct EliteSpawnInfo
{
    public bool isElite;
    public EliteAffix affix;
    public float hpMultiplier;
    public float damageMultiplier;
    public float attackSpeedMultiplier;
    public float moveSpeedMultiplier;
    public float scale;
    public int chestDrop;
    public Color tint;
}

/// <summary>精英词缀的可调常量，集中在这里，避免散落在各子类里。</summary>
public static class EliteAffixTuning
{
    public const int AffixVariants = 6;

    // 疾行
    public const float HasteSpeedBonus = 2.5f;
    public const float AfterimageInterval = 0.06f;
    public const float AfterimageLifetime = 0.3f;
    public const int AfterimageCapacity = 8;
    public static readonly Color AfterimageColor = new Color(0.06f, 0.06f, 0.08f, 0.55f);

    // 护盾（白色护盾条，先吃伤害，不做额外减伤）
    public const float ShieldRegenInterval = 2f;
    public const float ShieldRegenFraction = 0.20f;

    // 再生
    public const float RegenInterval = 1f;
    public const float RegenFraction = 0.15f;
    public const float RegenPulseSpeed = 3f;
    public static readonly Color RegenTint = new Color(0.62f, 1f, 0.62f, 1f);

    // 召唤
    public const float SummonInterval = 5f;
    public const float SummonSpawnRadius = 1.5f;

    // 分裂
    public const int SplitChildCount = 3;
    public const float SplitChildHealthFraction = 0.3f;
    public const float SplitChildSpawnRadius = 0.7f;

    // 死亡爆炸（先画范围预警 1 秒，再结算）
    // 预警圈大小与伤害判定共用这个半径，两者永远一致
    public const float DeathExplosionRadius = 1.2f;
    public const float DeathExplosionDamageScale = 4f;
    public const float DeathExplosionKnockback = 8f;
    public const float DeathExplosionDelay = 1f;
    public const float DeathExplosionFlashDuration = 0.18f;
    // 爆炸瞬间的屏幕震动（幅度 / 时长）
    public const float DeathExplosionShakeStrength = 0.45f;
    public const float DeathExplosionShakeDuration = 0.4f;

    // 分裂 / 召唤出来的小怪：从地面缓缓升起
    public const float EmergeHeight = 1.1f;
    public const float EmergeDuration = 0.55f;

    // 精英宝箱掉落概率（命中时按 eliteChestDrop 掉 N 只）
    public const float EliteChestDropChance = 0.5f;
}
