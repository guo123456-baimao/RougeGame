using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// 把局内 HUD 烘焙成真实场景物体（Tools/HUD 菜单）。
/// 烘焙后 In-Game HUD 及四个部件（Ability HUD / Boss Health Bar / Level Progress Bar / HUD Feedback）
/// 会以 GameObject + 序列化引用的形式存进 Level 1 场景，可以在 Scene/Game 视图和 Inspector
/// 里直接调整位置、锚点、颜色、字号——运行时 InGameHud 检测到引用已存在就不再重建，直接接管。
///
/// 配套说明：
/// - 烘焙用到的纯色 / 渐变 sprite 会落盘为 Assets/UI/Textures/HUD-White.png、HUD-Vignette.png，
///   否则运行时生成的临时 sprite 不保存，重开工程烘焙结果会丢图。
/// - 相位刻度、拾取文字内容、血条数值这类每帧被代码写入的东西，烘焙里调的值运行时会被覆盖；
///   位置、锚点、颜色、字号等静态属性则完全以烘焙结果为准。
/// - 想推倒重调：先执行「移除场景内 HUD 预览」再重新烘焙。
/// </summary>
public static class HudSceneBaker
{
    private const string HudNodeName = "In-Game HUD";
    private const string FallbackThemePath = "Assets/UI/HudTheme.asset";

    /// <summary>
    /// 把 HUD 结构烘焙成真实场景物体，便于在 Scene 与 Inspector 中直接调整。
    /// </summary>
    [MenuItem("Tools/HUD/烘焙到场景（可视化编辑）", priority = 20)]
    public static void Bake()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[HUD Baker] 请先退出 Play 模式再烘焙。");
            return;
        }

        EnsureSpriteAssets();

        Canvas canvas = FindGameCanvas();
        if (canvas == null)
        {
            Debug.LogError("[HUD Baker] 当前场景里找不到挂着 GameTimer 的局内 Canvas，请先打开 Level 1 场景。");
            return;
        }

        HudTheme theme = EnsureThemeAsset();

        // 确保根节点 + InGameHud 组件
        Transform hudNode = canvas.transform.Find(HudNodeName);
        InGameHud hud = hudNode != null ? hudNode.GetComponent<InGameHud>() : null;
        if (hud == null)
        {
            GameObject go = hudNode != null
                ? hudNode.gameObject
                : new GameObject(HudNodeName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Bake In-Game HUD");
            if (hudNode == null)
            {
                go.transform.SetParent(canvas.transform, false);
                go.transform.SetAsFirstSibling(); // 与运行时 Ensure() 保持同一渲染层级
            }
            hud = go.AddComponent<InGameHud>();
        }

        Undo.RecordObject(hud, "Bake In-Game HUD");
        hud.BakePartsInEditor(theme);
        EditorUtility.SetDirty(hud);

        // 部件内部的序列化引用（barRoot / vignette / slotContainer...）由 Build 写入，必须标脏才能落盘
        foreach (Transform child in hud.transform)
        {
            foreach (Component component in child.GetComponents<Component>())
                EditorUtility.SetDirty(component);
        }

        EditorSceneManager.MarkAllScenesDirty();
        AssetDatabase.SaveAssets();
        Debug.Log("[HUD Baker] 烘焙完成：In-Game HUD 已生成到场景，可在 Scene/Game 视图与 Inspector 里直接调整。");
    }

    /// <summary>
    /// 移除场景内烘焙出的 HUD 预览，恢复运行时自动构建。
    /// </summary>
    [MenuItem("Tools/HUD/移除场景内 HUD 预览", priority = 21)]
    public static void Remove()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[HUD Baker] 请先退出 Play 模式。");
            return;
        }

        Canvas canvas = FindGameCanvas();
        if (canvas == null)
        {
            Debug.LogError("[HUD Baker] 当前场景里找不到局内 Canvas。");
            return;
        }

        Transform hudNode = canvas.transform.Find(HudNodeName);
        if (hudNode != null)
        {
            Undo.DestroyObjectImmediate(hudNode.gameObject);
            Debug.Log("[HUD Baker] 已移除场景内 HUD 预览，运行时将恢复自动构建。");
        }

        // 技能示例槽位可能烘在画布里预留的 "Abilities" 容器下，一并清掉
        Transform abilities = FindDeep(canvas.transform, "Abilities");
        if (abilities != null)
        {
            for (int i = abilities.childCount - 1; i >= 0; i--)
            {
                Transform child = abilities.GetChild(i);
                if (child.name.StartsWith("Sample Slot"))
                    Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        EditorSceneManager.MarkAllScenesDirty();
    }

    /// <summary>
    /// 先移除旧预览再重新烘焙，覆盖上一次的调整。
    /// </summary>
    [MenuItem("Tools/HUD/重新烘焙（覆盖上次调整）", priority = 22)]
    public static void Rebake()
    {
        Remove();
        Bake();
    }

    // ------------------------------------------------------------------

    /// <summary>与运行时 FindGameCanvas 同规则：挂了 GameTimer 的画布才是局内画布。</summary>
    private static Canvas FindGameCanvas()
    {
        foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>(true))
        {
            if (canvas.GetComponentInChildren<GameTimer>(true) != null)
                return canvas;
        }
        return null;
    }

    /// <summary>
    /// 在层级树中递归查找指定名字的子节点。
    /// </summary>
    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>烘焙用 sprite 必须是落盘资产：纯色方块 + 径向渐变遮罩。</summary>
    private static void EnsureSpriteAssets()
    {
        if (AssetDatabase.LoadAssetAtPath<Sprite>(HudUiFactory.WhiteSpriteAssetPath) == null)
        {
            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            WriteSpriteAsset(HudUiFactory.WhiteSpriteAssetPath, tex);
        }

        if (AssetDatabase.LoadAssetAtPath<Sprite>(HudUiFactory.VignetteSpriteAssetPath) == null)
        {
            Texture2D tex = HudUiFactory.CreateVignetteTexture(128);
            WriteSpriteAsset(HudUiFactory.VignetteSpriteAssetPath, tex);
        }
    }

    /// <summary>
    /// 把生成的贴图写入指定路径并按 Sprite 规则导入。
    /// </summary>
    private static void WriteSpriteAsset(string path, Texture2D tex)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        Debug.Log("[HUD Baker] 已生成烘焙用 sprite 资产：" + path);
    }

    /// <summary>优先用工程里已有的 HudTheme 资产；没有就落一份本地副本（运行时主题走 Addressables，注意保持字段一致）。</summary>
    private static HudTheme EnsureThemeAsset()
    {
        string[] guids = AssetDatabase.FindAssets("t:HudTheme");
        if (guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<HudTheme>(AssetDatabase.GUIDToAssetPath(guids[0]));

        HudTheme theme = AssetDatabase.LoadAssetAtPath<HudTheme>(FallbackThemePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<HudTheme>();
            AssetDatabase.CreateAsset(theme, FallbackThemePath);
            Debug.LogWarning("[HUD Baker] 工程里没有 HudTheme 资产（运行时用的是 Addressables 加载的那份），"
                + "已创建本地副本 " + FallbackThemePath + "；如需与运行时主题一致，请对齐字段或把该资产注册进 Addressables。");
        }
        return theme;
    }
}
