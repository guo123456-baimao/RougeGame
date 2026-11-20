using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public class EngravingCreator : EditorWindow
{
    private const string EngravingFolder = "Assets/Blueprints/Engravings";
    private const string DatabasePath = EngravingFolder + "/EngravingDatabase.asset";

    /// <summary>
    /// 扫描所有 EngravingBlueprint 写入全局铭文库，新增铭文后点一次即可，
    /// 不必再逐个往角色蓝图上挂。
    /// </summary>
    [MenuItem("Tools/Engraving/刷新铭文数据库")]
    public static void RefreshEngravingDatabase()
    {
        var blueprints = new List<EngravingBlueprint>();
        foreach (var guid in AssetDatabase.FindAssets("t:EngravingBlueprint", new[] { EngravingFolder }))
        {
            var bp = AssetDatabase.LoadAssetAtPath<EngravingBlueprint>(AssetDatabase.GUIDToAssetPath(guid));
            if (bp != null) blueprints.Add(bp);
        }
        blueprints.Sort((a, b) => string.Compare(a.engravingId, b.engravingId, StringComparison.Ordinal));

        if (!AssetDatabase.IsValidFolder(EngravingFolder))
            AssetDatabase.CreateFolder("Assets/Blueprints", "Engravings");

        var database = AssetDatabase.LoadAssetAtPath<EngravingDatabase>(DatabasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<EngravingDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
        }

        database.allEngravings = blueprints.ToArray();
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        Debug.Log("[Engraving] 铭文数据库已刷新：" + blueprints.Count + " 个铭文 -> " + DatabasePath);
    }

    /// <summary>
    /// 生成一批示例铭文资源并保存到指定目录。
    /// </summary>
    [MenuItem("Tools/Engraving/Create Example Engravings")]
    public static void CreateExampleEngravings()
    {
        var examples = new[]
        {
            ("vampire_lifesteal", "吸血之痕", "攻击时 X% 几率恢复 Y 点生命", "诅咒: 受到伤害增加 30%", 500, 1f, 1f, 1f, 0.15f, 5f, 0f, 0f, 1f, 1.3f, 1f, 0f),
            ("vampire_damage", "狂暴印记", "伤害提升 40%", "诅咒: 最大生命值降低 30%", 400, 0.7f, 1.4f, 1f, 0f, 0f, 0f, 0f, 1f, 1f, 1f, 0f),
            ("vampire_tank", "铁壁铭文", "护甲 +5，生命 +15%", "诅咒: 移动速度降低 20%", 600, 1.15f, 1f, 1f, 0f, 0f, 5f, 0f, 1f, 1f, 0.8f, 0f),
            ("vampire_speed", "疾风刻印", "移动速度 +25%，伤害 +15%", "诅咒: 最大生命值降低 20%", 450, 0.8f, 1.15f, 1.25f, 0f, 0f, 0f, 0f, 1f, 1f, 1f, 0f),
            ("vampire_dogma", "虔诚祝福", "幸运 +3，回复效果 +20%", "诅咒: 护甲 -3", 350, 1f, 1f, 1f, 0f, 0f, -3f, 3f, 1f, 1f, 1f, 0f),
            ("vampire_risk", "赌徒契约", "伤害翻倍", "诅咒: 最大生命值变为 1%", 1000, 0.01f, 2f, 1f, 0f, 0f, 0f, 0f, 1f, 1f, 1f, 0f),
            ("vampire_balanced", "均衡之道", "全属性 +10%", "无诅咒", 800, 1.1f, 1.1f, 1.1f, 0f, 0f, 2f, 1f, 1f, 1f, 1f, 0f),
        };

        string dir = "Assets/Blueprints/Engravings";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/Blueprints", "Engravings");

        foreach (var (id, name, desc, curse, cost, hp, dmg, spd, lsChance, lsAmt, arm, luck, chp, cdmg, cspd, calm) in examples)
        {
            var path = $"{dir}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<EngravingBlueprint>(path) != null) continue;

            var engraving = ScriptableObject.CreateInstance<EngravingBlueprint>();
            engraving.engravingId = id;
            engraving.displayName = name;
            engraving.description = desc;
            engraving.curseDescription = curse;
            engraving.unlockCost = cost;
            engraving.hpMultiplier = hp;
            engraving.damageMultiplier = dmg;
            engraving.speedMultiplier = spd;
            engraving.lifestealChance = lsChance;
            engraving.lifestealAmount = lsAmt;
            engraving.armorValue = arm;
            engraving.luckValue = luck;
            engraving.curseHpMultiplier = chp;
            engraving.curseDamageMultiplier = cdmg;
            engraving.curseSpeedMultiplier = cspd;
            engraving.curseArmorValue = calm;

            AssetDatabase.CreateAsset(engraving, path);
            Debug.Log($"Created engraving: {name}");
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("成功", "已创建示例铭文！请分配图标和描述文本。", "OK");
    }
}
