using UnityEngine;
/// <summary>
/// 局内 HUD 总装。
/// 用法：把这个组件挂到 Level 1 场景的 UI Canvas 上即可，缺失的部件会在 Awake 里按主题自动补齐，
/// 不需要手改场景层级。已在场景里摆好的部件也可以直接拖进来接管。
/// LevelManager.Init 会调用 <see cref="Bind"/> 把运行时依赖注入进来。
/// </summary>
[DefaultExecutionOrder(-100)]
public class InGameHud : MonoBehaviour
{
    public static InGameHud Instance { get; private set; }

    [Header("Theme")]
    [SerializeField] private HudTheme theme;

    [Header("Existing Parts (optional)")]
    [SerializeField] private AbilityHud abilityHud;
    [SerializeField] private BossHealthBar bossHealthBar;
    [SerializeField] private LevelProgressBar levelProgressBar;
    [SerializeField] private HudFeedback feedback;

    [Header("Auto Build")]
    [SerializeField] private bool buildMissingParts = true;
    [SerializeField] private bool buildAbilityHud = true;
    [SerializeField] private bool buildBossBar = true;
    [SerializeField] private bool buildProgressBar = true;
    [SerializeField] private bool buildFeedback = true;

    public HudTheme Theme { get => theme; set => theme = value; }

    /// <summary>
    /// 拿到局内 HUD。场景里没挂就自动在局内 Canvas 下建一个——
    /// 这样整套 HUD 不需要手改 Level 1.unity 就能生效。
    /// </summary>
    public static InGameHud Ensure()
    {
        if (Instance != null) return Instance;

        Canvas canvas = FindGameCanvas();
        if (canvas == null) return null;

        GameObject go = new GameObject("In-Game HUD", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        go.transform.SetAsFirstSibling();

        return go.AddComponent<InGameHud>();
    }

    /// <summary>认准局内画布：上面挂着 GameTimer 的那个（主菜单的 Canvas 没有）。</summary>
    private static Canvas FindGameCanvas()
    {
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].GetComponentInChildren<GameTimer>(true) != null)
                return canvases[i];
        }
        return null;
    }

    /// <summary>
    /// 注册为单例、套用默认主题，并按开关补建缺失的 HUD 部件。
    /// </summary>
    private void Awake()
    {
        Instance = this;
        if (theme == null)
            theme = HudTheme.Default;

        if (buildMissingParts)
            BuildMissingParts();
    }

    /// <summary>
    /// 销毁时清空单例引用。
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 按开关补建技能栏、Boss 血条、进度条与反馈层。
    /// </summary>
    private void BuildMissingParts()
    {
        if (abilityHud == null && buildAbilityHud)
            abilityHud = CreateChild<AbilityHud>("Ability HUD");
        if (bossHealthBar == null && buildBossBar)
            bossHealthBar = CreateChild<BossHealthBar>("Boss Health Bar");
        if (levelProgressBar == null && buildProgressBar)
            levelProgressBar = CreateChild<LevelProgressBar>("Level Progress Bar");
        if (feedback == null && buildFeedback)
            feedback = CreateChild<HudFeedback>("HUD Feedback");
    }

    private T CreateChild<T>(string name) where T : Component
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        T component = go.AddComponent<T>();
        ApplyTheme(component);
        return component;
    }

    /// <summary>
    /// 按部件类型把当前 HUD 主题分发下去。
    /// </summary>
    private void ApplyTheme(Component component)
    {
        switch (component)
        {
            case AbilityHud a: a.Theme = theme; break;
            case BossHealthBar b: b.SetTheme(theme); break;
            case LevelProgressBar p: p.SetTheme(theme); break;
            case HudFeedback f: f.SetTheme(theme); break;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器烘焙总装（Tools/HUD/烘焙到场景 调用）：
    /// 把缺失的四个部件建成真实场景物体并逐个烘焙 UI，序列化引用落盘后，
    /// 运行时 Awake 检测到引用已存在就不再重建，直接接管烘焙结果——
    /// 于是 HUD 在 Scene/Game 视图和 Inspector 里可见、可调。
    /// </summary>
    public void BakePartsInEditor(HudTheme editorTheme)
    {
        if (Application.isPlaying) return;
        if (editorTheme != null) theme = editorTheme;

        if (abilityHud == null) abilityHud = CreateChild<AbilityHud>("Ability HUD");
        if (bossHealthBar == null) bossHealthBar = CreateChild<BossHealthBar>("Boss Health Bar");
        if (levelProgressBar == null) levelProgressBar = CreateChild<LevelProgressBar>("Level Progress Bar");
        if (feedback == null) feedback = CreateChild<HudFeedback>("HUD Feedback");

        abilityHud.BuildForEditor();
        bossHealthBar.BuildForEditor();
        levelProgressBar.BuildForEditor();
        feedback.BuildForEditor();
    }
#endif

    /// <summary>由 LevelManager.Init 调用。</summary>
    public void Bind(LevelBlueprint levelBlueprint, Character playerCharacter, AbilityManager abilityManager, EntityManager entityManager)
    {
        abilityHud?.Bind(abilityManager);
        levelProgressBar?.Bind(levelBlueprint);
        feedback?.Bind(playerCharacter);
    }

    // ===== 转发给各子部件，调用方不需要知道内部分工 =====

    public void SetLevelTime(float t) => levelProgressBar?.SetTime(t);

    public void ShowBoss(Monster monster) => bossHealthBar?.Show(monster);

    public void HideBoss() => bossHealthBar?.Hide();

    public void FlashDamage() => feedback?.FlashDamage();

    public void FlashLevelUp() => feedback?.FlashLevelUp();

    /// <summary>
    /// 把拾取飘字转发给反馈层显示。
    /// </summary>
    public void ShowPickupText(string content, Color color, Vector3 worldPosition)
        => feedback?.ShowPickupText(content, color, worldPosition);

    public void RefreshAbilities() => abilityHud?.Refresh();
}
