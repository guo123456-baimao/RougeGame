using TMPro;
using UnityEngine;
/// <summary>
/// 局内 HUD 的设计令牌：配色、动效时长、排版尺寸集中在这里，
/// 避免每个脚本各写一套硬编码数值导致风格分裂。
/// 不指定时走 <see cref="Default"/>（字段初始值），保证零配置也能跑。
/// </summary>
[CreateAssetMenu(fileName = "HudTheme", menuName = "RougeGame/UI/HUD Theme", order = 10)]
public class HudTheme : ScriptableObject
{
    [Header("Bars")]
    public Color healthFill = new Color(0.86f, 0.20f, 0.26f, 1f);
    public Color healthLag = new Color(1f, 0.82f, 0.35f, 1f);
    public Color expFill = new Color(0.24f, 0.78f, 0.94f, 1f);
    public Color expLag = new Color(0.75f, 0.95f, 1f, 0.9f);
    public Color bossFill = new Color(0.92f, 0.30f, 0.18f, 1f);
    public Color bossLag = new Color(1f, 0.74f, 0.28f, 1f);
    public Color progressFill = new Color(0.55f, 0.86f, 0.45f, 1f);
    public Color trackColor = new Color(0.05f, 0.06f, 0.09f, 0.78f);
    public Color frameColor = new Color(1f, 1f, 1f, 0.18f);

    [Header("Text")]
    public Color textColor = new Color(0.96f, 0.97f, 1f, 1f);
    public Color textDimColor = new Color(0.72f, 0.76f, 0.84f, 1f);
    public Color warningColor = new Color(1f, 0.32f, 0.32f, 1f);
    public Color coinColor = new Color(1f, 0.83f, 0.30f, 1f);
    public Color expColor = new Color(0.45f, 0.90f, 1f, 1f);
    public Color healColor = new Color(0.45f, 0.95f, 0.55f, 1f);

    [Header("Overlay")]
    [Tooltip("受击/低血量时屏幕边缘泛红的颜色")]
    public Color damageVignette = new Color(1f, 0.12f, 0.16f, 1f);
    public Color levelUpFlash = new Color(1f, 0.94f, 0.72f, 1f);

    [Header("Ability Slots")]
    public Color slotFrame = new Color(1f, 1f, 1f, 0.22f);
    public Color slotBackground = new Color(0.06f, 0.07f, 0.11f, 0.85f);
    public Color cooldownMask = new Color(0f, 0f, 0f, 0.68f);
    public float slotSize = 64f;
    public float slotSpacing = 10f;

    [Header("Motion")]
    [Tooltip("延迟条：受伤后主条先掉，延迟条停留一会儿再追上去")]
    public float lagBarDelay = 0.16f;
    [Tooltip("延迟条追赶速度，单位是「整条/秒」")]
    public float lagBarSpeed = 1.6f;
    public float lowHealthThreshold = 0.25f;
    public float lowHealthPulseSpeed = 5f;
    public float pickupTextDuration = 0.85f;
    public float pickupTextRise = 72f;
    public float damageFlashDuration = 0.38f;
    public float levelUpFlashDuration = 0.5f;
    public float slotPopDuration = 0.3f;
    public float barAppearDuration = 0.25f;

    [Header("Labels")]
    [Tooltip("运行时创建的文本默认走 TMP 全局字体（LiberationSans，无中文字形）。\n想用中文标签就把中文字体资源拖到这里。")]
    public TMP_FontAsset fontOverride;
    public string bossLabel = "BOSS";
    public string timeToBossLabel = "TO BOSS ";

    /// <summary>主题资产的可寻址地址（编辑器迁移脚本会把 Resources/UI/HUD Theme.asset 标记为该地址）。</summary>
    public const string DefaultThemeAddress = "UI/HUD Theme";

    private static HudTheme defaultTheme;
    private static HudTheme fallbackTheme;
    private static bool loadRequested;

    /// <summary>
    /// 默认主题：通过 Addressables 异步加载（常驻资源，整局不释放），
    /// 加载完成前用字段初始值兜底，保证零配置也能跑。
    /// LevelManager 初始化链路会预先等待加载完成，HUD 构建时拿到的就是真主题。
    /// </summary>
    public static HudTheme Default
    {
        get
        {
            if (defaultTheme == null)
            {
                if (!loadRequested)
                {
                    loadRequested = true;
                    ResManager.LoadPersistentAsync<HudTheme>(DefaultThemeAddress, SetDefault);
                }
                if (defaultTheme == null)
                {
                    if (fallbackTheme == null)
                        fallbackTheme = CreateInstance<HudTheme>();
                    return fallbackTheme;
                }
            }
            return defaultTheme;
        }
    }

    /// <summary>ResManager 加载完成后回填真实主题。</summary>
    private static void SetDefault(HudTheme theme)
    {
        if (theme != null)
            defaultTheme = theme;
    }

    /// <summary>当前生效的主题：优先用场景里 InGameHud 挂的那份，否则用默认。</summary>
    public static HudTheme Active
    {
        get
        {
            if (InGameHud.Instance != null && InGameHud.Instance.Theme != null)
                return InGameHud.Instance.Theme;
            return Default;
        }
    }

    public TMP_FontAsset Font => fontOverride;
}
