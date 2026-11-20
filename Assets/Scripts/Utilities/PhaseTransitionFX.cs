using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// 相位切换演出：屏幕渐黑 + 玩家下沉 → 屏幕全黑窗口期显示「你已坠入{相位名}！！！」
/// （LevelManager 在边界帧完成背景与刷怪表切换）→ 屏幕渐亮 + 玩家上升。
/// 渐黑 1.0s + 全黑展示 1.5s + 渐亮 1.0s，全程 3.5s。
/// 只做视觉演出：不动玩家物理根节点、不动相机、不清场上的怪。
/// 由 LevelManager 在 Init 时创建，随场景销毁重建。
/// </summary>
public class PhaseTransitionFX : MonoBehaviour
{
    public const float FadeOutDuration = 1.0f;   // 渐黑 + 下沉
    public const float BlackHoldDuration = 1.5f; // 全黑展示提示文字
    public const float FadeInDuration = 1.0f;    // 渐亮 + 上升
    private const float SinkDepth = 1.2f;        // 玩家精灵下沉的视觉距离（世界单位）

    private Image blackOverlay;
    private TextMeshProUGUI phaseText;           // 全黑窗口期的提示文字
    private Transform playerVisual;              // 玩家精灵节点（只动视觉子节点，不动物理根）
    private Vector3 playerVisualBaseLocalPos;
    private SpriteRenderer[] playerRenderers;    // 玩家所有精灵渲染器（下坠时同步淡出）
    private Color[] playerBaseColors;            // 各渲染器的原始颜色

    /// <summary>运行时创建全屏黑色遮罩 + 提示文字并绑定玩家精灵，返回演出组件。</summary>
    public static PhaseTransitionFX Create(Transform playerRoot)
    {
        GameObject go = new GameObject("Phase Transition FX");
        PhaseTransitionFX fx = go.AddComponent<PhaseTransitionFX>();
        fx.BuildOverlay();
        fx.BindPlayer(playerRoot);
        return fx;
    }

    /// <summary>
    /// 构建全屏黑色遮罩，用于相位切换转场。
    /// </summary>
    private void BuildOverlay()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;  // 盖在局内 HUD 之上
        blackOverlay = HudUiFactory.CreateImage("Black", transform, new Color(0f, 0f, 0f, 0f));
        RectTransform rect = blackOverlay.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // 提示文字：挂在遮罩上，默认隐藏；用主题中文字体避免缺字形
        HudTheme t = HudTheme.Active;
        phaseText = HudUiFactory.CreateText("Phase Text", transform, string.Empty, 44f,
            t != null ? t.textColor : Color.white, TextAlignmentOptions.Center, t != null ? t.Font : null);
        RectTransform textRect = phaseText.rectTransform;
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(900f, 120f);
        textRect.anchoredPosition = Vector2.zero;
        phaseText.gameObject.SetActive(false);
    }

    /// <summary>
    /// 绑定玩家，以便转场时临时下沉/隐藏角色表现。
    /// </summary>
    private void BindPlayer(Transform playerRoot)
    {
        playerRenderers = playerRoot.GetComponentsInChildren<SpriteRenderer>();
        playerBaseColors = new Color[playerRenderers.Length];
        for (int i = 0; i < playerRenderers.Length; i++)
            playerBaseColors[i] = playerRenderers[i].color;

        SpriteRenderer renderer = playerRoot.GetComponentInChildren<SpriteRenderer>();
        playerVisual = renderer != null ? renderer.transform : playerRoot;
        playerVisualBaseLocalPos = playerVisual.localPosition;
    }

    /// <summary>
    /// 播放一次切换演出。在边界前 FadeOutDuration 调用，屏幕全黑时正好跨过相位边界。
    /// </summary>
    public IEnumerator Transition(string phaseDisplayName)
    {
        // 渐黑 + 下沉 + 玩家逐渐透明
        float t = 0;
        while (t < FadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / FadeOutDuration);
            SetBlackAlpha(k);
            ApplySink(k);
            ApplyFade(1f - k);
            yield return null;
        }
        SetBlackAlpha(1f);
        ApplySink(1f);
        ApplyFade(0f);

        // 屏幕全黑窗口期：显示提示文字；背景切换发生在边界帧（由 LevelManager.UpdatePhase 完成）
        if (phaseText != null)
        {
            phaseText.text = $"你已坠入{phaseDisplayName}！！！";
            phaseText.gameObject.SetActive(true);
        }
        t = 0;
        while (t < BlackHoldDuration)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // 渐亮 + 上升 + 玩家逐渐恢复：文字随黑幕一起淡出
        t = 0;
        while (t < FadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / FadeInDuration);
            SetBlackAlpha(1f - k);
            ApplySink(1f - k);
            ApplyFade(k);
            if (phaseText != null)
            {
                Color c = phaseText.color;
                c.a = 1f - Mathf.Clamp01(k * 2f);  // 渐亮前半段文字先淡出
                phaseText.color = c;
                if (c.a <= 0f) phaseText.gameObject.SetActive(false);
            }
            yield return null;
        }
        SetBlackAlpha(0f);
        if (phaseText != null) phaseText.gameObject.SetActive(false);
        RestorePlayerVisual();
    }

    /// <summary>
    /// 设置转场遮罩的透明度。
    /// </summary>
    private void SetBlackAlpha(float alpha)
    {
        if (blackOverlay != null)
            blackOverlay.color = new Color(0f, 0f, 0f, alpha);
    }

    /// <summary>
    /// 按进度让玩家的视觉表现下沉。
    /// </summary>
    private void ApplySink(float k)
    {
        if (playerVisual != null)
            playerVisual.localPosition = playerVisualBaseLocalPos + Vector3.down * (SinkDepth * k);
    }

    /// <summary>按比例设置玩家全部精灵的透明度（1=原始，0=完全透明）。</summary>
    private void ApplyFade(float alpha)
    {
        if (playerRenderers == null) return;
        for (int i = 0; i < playerRenderers.Length; i++)
        {
            if (playerRenderers[i] == null) continue;
            Color c = playerBaseColors[i];
            c.a *= alpha;
            playerRenderers[i].color = c;
        }
    }

    /// <summary>
    /// 恢复玩家原本的渲染状态。
    /// </summary>
    private void RestorePlayerVisual()
    {
        if (playerVisual != null)
            playerVisual.localPosition = playerVisualBaseLocalPos;
        ApplyFade(1f);  // 恢复原始透明度
    }

    /// <summary>
    /// 销毁时清理运行时创建的遮罩对象。
    /// </summary>
    private void OnDestroy()
    {
        // 场景重建/中途退出时恢复玩家精灵位置，避免残留下沉偏移
        RestorePlayerVisual();
        // 若场景在演出中途被卸载（重开/返回主菜单），兜底恢复时间流速，防止卡死在 0
        Time.timeScale = 1f;
    }
}
