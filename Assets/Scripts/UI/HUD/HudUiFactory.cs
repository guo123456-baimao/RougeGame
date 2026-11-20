using TMPro;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// 运行时构建 uGUI 的小工具。
/// 存在意义：局内 HUD 的不少部件（技能槽、Boss 血条、进度条）在场景里是缺失的，
/// 与其手改 .unity 文件，不如在 Awake 里按主题自动补齐——用户拖一个组件就能看到效果。
/// </summary>
public static class HudUiFactory
{
    /// <summary>烘焙进场景的 HUD 图片统一用这两个 PNG 资产当 sprite，
    /// 否则运行时生成的临时 sprite 不落盘，烘焙结果一重开就丢图。
    /// 由 Tools/HUD/烘焙到场景 菜单自动生成；缺失时编辑器内回退运行时生成。</summary>
    public const string WhiteSpriteAssetPath = "Assets/UI/Textures/HUD-White.png";
    public const string VignetteSpriteAssetPath = "Assets/UI/Textures/HUD-Vignette.png";

    private static Sprite whiteSprite;

    public static Sprite WhiteSprite
    {
        get
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Sprite baked = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpriteAssetPath);
                if (baked != null) return baked;
            }
#endif
            if (whiteSprite == null)
            {
                Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                Color32[] pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = Color.white;
                tex.SetPixels32(pixels);
                tex.Apply(false, true);
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
                whiteSprite.name = "HudWhite";
            }
            return whiteSprite;
        }
    }

    /// <summary>
    /// 创建一个带 RectTransform 的空节点并挂到父节点下。
    /// </summary>
    public static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    /// <summary>撑满父节点。</summary>
    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>以父节点某个锚点定位的固定尺寸矩形。</summary>
    public static RectTransform CreateAnchored(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 anchoredPosition)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        return rect;
    }

    public static Image CreateImage(string name, Transform parent, Color color, Sprite sprite = null, bool raycastTarget = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite != null ? sprite : WhiteSprite;
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    /// <summary>
    /// 创建一个填充型 Image，用于血条/经验条等进度显示。
    /// </summary>
    public static Image CreateFilledImage(string name, Transform parent, Color color, Image.FillMethod fillMethod)
    {
        Image image = CreateImage(name, parent, color);
        image.type = Image.Type.Filled;
        image.fillMethod = fillMethod;
        image.fillOrigin = (int)Image.Origin360.Top;
        image.fillClockwise = true;
        image.fillAmount = 0f;
        return image;
    }

    /// <summary>
    /// 创建一个 TextMeshPro 文本节点并设置字号、颜色与对齐。
    /// </summary>
    public static TextMeshProUGUI CreateText(string name, Transform parent, string content, float fontSize, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center, TMP_FontAsset font = null, bool raycastTarget = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset fontAsset = font != null ? font : TMP_Settings.defaultFontAsset;
        if (fontAsset != null)
            tmp.font = fontAsset;
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = raycastTarget;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    /// <summary>带描边的文本，HUD 上叠在杂乱背景时可读性更好。</summary>
    public static void ApplyOutline(TextMeshProUGUI text, Color color, float thickness = 0.22f)
    {
        if (text == null) return;
        text.outlineWidth = thickness;
        text.outlineColor = color;
    }

    /// <summary>
    /// 生成中间透明、四周渐显的径向渐变纹理，用于受击/低血量的屏幕边缘泛红。
    /// 编辑器内优先取烘焙好的 PNG 资产（烘焙结果重开不丢图），运行时照旧现场生成。
    /// </summary>
    public static Sprite CreateVignetteSprite(int size = 128)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Sprite baked = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(VignetteSpriteAssetPath);
            if (baked != null) return baked;
        }
#endif
        Texture2D tex = CreateVignetteTexture(size);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "HudVignette";
        return sprite;
    }

    /// <summary>与 CreateVignetteSprite 同一套渐变算法的纯纹理版，烘焙工具用它导出 PNG 资产。</summary>
    public static Texture2D CreateVignetteTexture(int size = 128)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size * 0.5f;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = Mathf.SmoothStep(0.42f, 1f, d);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        // 保持 CPU 可读：烘焙工具需要 EncodeToPNG 导出资产（旧参数会标记不可读导致导出失败）
        tex.Apply(false, false);
        return tex;
    }
}
