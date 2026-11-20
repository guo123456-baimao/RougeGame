using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
/// <summary>
/// Addressables 一次性迁移工具（菜单：RougeGame/Addressables 迁移）。
///
/// 背景：monster/boss/expGem/coin/chest/text 预制体原来是 GameObject 直接引用，
/// 代码已改为 AssetReferenceGameObject，直接改类型会丢序列化数据。
/// 本工具读取迁移前备份的 GUID（addressables_migration_backup.json）回填，
/// 并把相关资产标记为可寻址、给 EntityManager 注册全量 CollectableType。
/// 可重复执行（幂等）：已填好的引用不会被覆盖。
/// </summary>
public static class AddressablesMigrationTool
{
    private const string BackupPath = "Assets/Editor/addressables_migration_backup.json";
    private const string LevelScenePath = "Assets/Scenes/Game/Level 1.unity";

    /// <summary>
    /// 一键把预制体与场景引用迁移到 Addressables，并生成可回滚的备份。
    /// </summary>
    [MenuItem("RougeGame/Addressables 迁移（一键）")]
    public static void Migrate()
    {
        if (!File.Exists(BackupPath))
        {
            Debug.LogError($"[AddressablesMigration] 找不到备份文件 {BackupPath}，无法回填旧引用");
            return;
        }

        var backup = JsonUtility.FromJson<BackupData>(File.ReadAllText(BackupPath));

        // 1. 确保 Addressables 设置与默认分组存在，并切到「Use Asset Database (fastest)」
        //    （新建设置默认可能落在 index 3 = Use Existing Build，Play 模式会报 RuntimeData is null）
        var settings = EnsureAddressablesSettings();
        if (settings.ActivePlayerDataBuilderIndex != 0)
        {
            settings.ActivePlayerDataBuilderIndex = 0;  // BuildScriptFastMode：编辑器下直接读资产库，无需先打包
            Debug.Log("[AddressablesMigration] Play Mode Script 已切换为 Use Asset Database (fastest)");
        }

        // 2. 标记资产为可寻址
        MarkAddressable(settings, backup.hudThemeGuid, HudTheme.DefaultThemeAddress);
        foreach (string guid in backup.level.monstersPrefabGuids)
            MarkAddressable(settings, guid);
        if (!string.IsNullOrEmpty(backup.level.finalBossPrefabGuid))
            MarkAddressable(settings, backup.level.finalBossPrefabGuid);
        foreach (string guid in backup.level.dropPrefabGuids)
            MarkAddressable(settings, guid);

        // 3. 回填 LevelBlueprint（Level 1.asset）
        MigrateLevelBlueprint(backup);

        // 4. 回填 CollectableType.itemPrefab（原来是 null，顺带补上对应预制体）
        MigrateCollectableTypes(settings);

        // 5. 打开游戏场景：回填 EntityManager 的 4 个掉落物 prefab + 注册 CollectableType 列表
        MigrateScene(backup);

        AssetDatabase.SaveAssets();
        Debug.Log("[AddressablesMigration] 迁移完成。请在 Addressables Groups 窗口核对 Default Local Group。");
    }

    /// <summary>
    /// 确保 Addressables 设置资源存在，不存在则创建。
    /// </summary>
    private static AddressableAssetSettings EnsureAddressablesSettings()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Directory.CreateDirectory("Assets/AddressableAssetsData");
            // 注意：Create 只建资产，必须再注册为默认设置对象（GUI 菜单创建流程才会做这一步）
            settings = AddressableAssetSettings.Create(
                "Assets/AddressableAssetsData", "AddressableAssetSettings", true, true);
            AddressableAssetSettingsDefaultObject.Settings = settings;
            Debug.Log("[AddressablesMigration] 已创建并注册 AddressableAssetSettings");
        }
        return settings;
    }

    private static void MarkAddressable(AddressableAssetSettings settings, string guid, string address = null)
    {
        if (string.IsNullOrEmpty(guid) || AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid)) == null)
        {
            if (!string.IsNullOrEmpty(guid))
                Debug.LogWarning($"[AddressablesMigration] GUID {guid} 对应资产不存在，跳过标记");
            return;
        }

        settings.CreateOrMoveEntry(guid, settings.DefaultGroup, false, true);
        var entry = settings.FindAssetEntry(guid);
        if (!string.IsNullOrEmpty(address) && entry != null && entry.address != address)
            entry.SetAddress(address, false);
    }

    /// <summary>
    /// 把关卡蓝图里的怪物/掉落预制体引用改为 Addressable 引用。
    /// </summary>
    private static void MigrateLevelBlueprint(BackupData backup)
    {
        string[] levelGuids = AssetDatabase.FindAssets("t:LevelBlueprint");
        foreach (string levelGuid in levelGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(levelGuid);
            var blueprint = AssetDatabase.LoadAssetAtPath<LevelBlueprint>(path);
            if (blueprint == null) continue;

            bool dirty = false;
            var so = new SerializedObject(blueprint);

            // monsters[].monstersPrefab
            var monsters = so.FindProperty("monsters");
            for (int i = 0; i < monsters.arraySize && i < backup.level.monstersPrefabGuids.Length; i++)
            {
                var prop = monsters.GetArrayElementAtIndex(i).FindPropertyRelative("monstersPrefab");
                if (SetAssetReference(prop, backup.level.monstersPrefabGuids[i]))
                    dirty = true;
            }

            // finalBoss.bossPrefab
            var finalBoss = so.FindProperty("finalBoss");
            if (SetAssetReference(finalBoss.FindPropertyRelative("bossPrefab"), backup.level.finalBossPrefabGuid))
                dirty = true;

            // miniBosses[] 与 phases[].phaseMiniBoss 的 bossPrefab 同属 Boss 池，指向同一预制体
            var miniBosses = so.FindProperty("miniBosses");
            for (int i = 0; i < miniBosses.arraySize; i++)
            {
                if (SetAssetReference(miniBosses.GetArrayElementAtIndex(i).FindPropertyRelative("bossPrefab"), backup.level.finalBossPrefabGuid))
                    dirty = true;
            }
            var phases = so.FindProperty("phases");
            for (int i = 0; i < phases.arraySize; i++)
            {
                var mini = phases.GetArrayElementAtIndex(i).FindPropertyRelative("phaseMiniBoss");
                if (mini != null && SetAssetReference(mini.FindPropertyRelative("bossPrefab"), backup.level.finalBossPrefabGuid))
                    dirty = true;
            }

            if (dirty)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(blueprint);
                Debug.Log($"[AddressablesMigration] 已回填 LevelBlueprint：{path}");
            }
        }
    }

    /// <summary>
    /// 为所有拾取物预制体注册 Addressable 条目。
    /// </summary>
    private static void MigrateCollectableTypes(AddressableAssetSettings settings)
    {
        // 预制体组件名 ↔ CollectableType 资产名匹配（去空格、不区分大小写）
        string[] collectableTypeGuids = AssetDatabase.FindAssets("t:CollectableType");
        var prefabCache = new Dictionary<string, string>();  // 规范化组件名 → prefab GUID
        foreach (string prefabGuid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null) continue;
            foreach (var component in prefabAsset.GetComponentsInChildren<Collectable>(true))
            {
                string normalized = component.GetType().Name.Replace(" ", "").ToLowerInvariant();
                prefabCache[normalized] = prefabGuid;
            }
        }

        foreach (string typeGuid in collectableTypeGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(typeGuid);
            var typeAsset = AssetDatabase.LoadAssetAtPath<CollectableType>(path);
            if (typeAsset == null) continue;

            string normalized = typeAsset.name.Replace(" ", "").ToLowerInvariant();
            if (!prefabCache.TryGetValue(normalized, out string prefabGuid))
            {
                Debug.LogWarning($"[AddressablesMigration] 找不到与 {typeAsset.name} 匹配的拾取物预制体，跳过");
                continue;
            }

            var so = new SerializedObject(typeAsset);
            var prop = so.FindProperty("itemPrefab");
            if (SetAssetReference(prop, prefabGuid))
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(typeAsset);
            }
            MarkAddressable(settings, prefabGuid);
            Debug.Log($"[AddressablesMigration] {typeAsset.name} → {AssetDatabase.GUIDToAssetPath(prefabGuid)}");
        }
    }

    /// <summary>
    /// 处理场景中的直接引用，改为运行时通过 Addressables 加载。
    /// </summary>
    private static void MigrateScene(BackupData backup)
    {
        var scene = EditorSceneManager.OpenScene(LevelScenePath, OpenSceneMode.Single);
        EntityManager entityManager = Object.FindObjectOfType<EntityManager>();
        if (entityManager == null)
        {
            Debug.LogError($"[AddressablesMigration] 场景 {LevelScenePath} 中找不到 EntityManager");
            return;
        }

        var so = new SerializedObject(entityManager);
        bool dirty = false;

        dirty |= SetAssetReference(so.FindProperty("expGemPrefab"), backup.level.dropPrefabGuids.Length > 0 ? backup.level.dropPrefabGuids[0] : null);
        dirty |= SetAssetReference(so.FindProperty("coinPrefab"), backup.level.dropPrefabGuids.Length > 1 ? backup.level.dropPrefabGuids[1] : null);
        dirty |= SetAssetReference(so.FindProperty("chestPrefab"), backup.level.dropPrefabGuids.Length > 2 ? backup.level.dropPrefabGuids[2] : null);
        dirty |= SetAssetReference(so.FindProperty("textPrefab"), backup.level.dropPrefabGuids.Length > 3 ? backup.level.dropPrefabGuids[3] : null);

        // 注册全量 CollectableType
        var typesProp = so.FindProperty("collectableTypes");
        string[] typeGuids = AssetDatabase.FindAssets("t:CollectableType");
        typesProp.arraySize = typeGuids.Length;
        for (int i = 0; i < typeGuids.Length; i++)
        {
            var asset = AssetDatabase.LoadAssetAtPath<CollectableType>(AssetDatabase.GUIDToAssetPath(typeGuids[i]));
            typesProp.GetArrayElementAtIndex(i).objectReferenceValue = asset;
        }
        dirty = true;

        if (dirty)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(entityManager);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[AddressablesMigration] 已回填场景 EntityManager 并保存场景");
        }
    }

    /// <summary>把 GUID 写入 AssetReference 序列化属性；已有值时跳过（幂等）。</summary>
    private static bool SetAssetReference(SerializedProperty prop, string guid)
    {
        if (prop == null)
        {
            Debug.LogError("[AddressablesMigration] 序列化属性缺失，请确认代码已改为 AssetReferenceGameObject");
            return false;
        }
        var guidProp = prop.FindPropertyRelative("m_AssetGUID");
        if (guidProp == null)
        {
            Debug.LogError("[AddressablesMigration] 属性不是 AssetReference 结构（缺 m_AssetGUID）");
            return false;
        }
        if (!string.IsNullOrEmpty(guidProp.stringValue))
            return false;  // 已有值，保持幂等
        if (string.IsNullOrEmpty(guid))
            return false;
        guidProp.stringValue = guid;
        return true;
    }

    [System.Serializable]
    private class BackupData
    {
        public LevelBackup level;
        public CollectableBackup[] collectables;
        public string hudThemeGuid;
    }

    [System.Serializable]
    private class LevelBackup
    {
        public string path;
        public string[] monstersPrefabGuids;
        public string finalBossPrefabGuid;
        public string[] dropPrefabGuids;
    }

    [System.Serializable]
    private class CollectableBackup
    {
        public string asset;
        public string assetGuid;
        public string itemPrefabGuid;
    }
}
