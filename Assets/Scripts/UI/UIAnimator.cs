using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
/// <summary>
/// UI Toolkit 动效工具层。
/// PlayEnter：让页面上带 .anim 的静态容器依次淡入 + 上浮。
/// USS 不支持 @keyframes，一次性动画只能由 C# 切换状态类驱动。
/// 必须在 TryRefresh / TryBind 成功、可视树已建好之后再调用。
///
/// 其余方法（NumberTo / PopIn / CrossFadePages / Shake）是通用补间工具，
/// 全部基于 schedule，返回 IVisualElementScheduledItem 句柄，调用方可 Pause 掉。
///
/// 头号坑：运行时写内联 style 会永久压过 USS（.lift 投影、hover 位移都会失效），
/// 所以凡是在这里写了 style.scale / opacity / translate 的，结束时一律复位成
/// StyleKeyword.Null，把控制权交还给 USS。
/// </summary>
public static class UIAnimator
{
    private const string AnimClass = "anim";
    private const string HiddenClass = "anim-hidden";

    // 首页帧必须先跑一次样式解析，元素才会真的停在 .anim-hidden 上；
    // 同帧 Add 再 Remove 不会触发任何过渡。
    private const long FirstFrameDelayMs = 16;
    private const long StaggerStepMs = 80;

    // 已经播过一次的树。页面失活时 UIDocument 会拆掉可视树（panel 变 null），
    // 重新激活时是一棵新树，所以这里只挡「同一棵树被 Refresh 反复调用」的情况。
    private static readonly HashSet<VisualElement> played = new HashSet<VisualElement>();

    /// <summary>
    /// 让 root 下所有带 .anim 的元素依次淡入 + 上浮。
    /// 同一棵可视树只播一次，重复调用（例如 EngravingManager.OnSelectedChanged
    /// 触发的 Refresh）不会让页面反复闪。
    /// </summary>
    public static void PlayEnter(VisualElement root)
    {
        if (root == null || root.panel == null) return;

        PrunePlayed();

        var targets = new List<VisualElement>();
        root.Query<VisualElement>(null, AnimClass).ForEach(e => targets.Add(e));
        if (targets.Count == 0) return;

        // 已经在这棵树上播过：只保证状态是「已入场」，不再重播
        if (!played.Add(root))
        {
            foreach (var done in targets) done.RemoveFromClassList(HiddenClass);
            return;
        }

        // 先压回初始态，避免页面反复激活时状态叠加
        foreach (var el in targets) el.AddToClassList(HiddenClass);

        foreach (var el in targets)
        {
            long delay = FirstFrameDelayMs + DelayIndex(el) * StaggerStepMs;
            var target = el;
            target.schedule.Execute(() => target.RemoveFromClassList(HiddenClass))
                .ExecuteLater(delay);
        }
    }

    /// <summary>清掉已经失效（可视树被拆掉）的记录，避免长引用常驻。</summary>
    private static void PrunePlayed()
    {
        if (played.Count == 0) return;
        played.RemoveWhere(e => e == null || e.panel == null);
    }

    /// <summary>读 d1~d4 档位，没标档位的元素视为第 0 档（最先入场）。</summary>
    private static long DelayIndex(VisualElement el)
    {
        for (int i = 1; i <= 4; i++)
        {
            if (el.ClassListContains("d" + i)) return i;
        }
        return 0;
    }

    // ===================== 通用补间工具 =====================
    // 30fps 的步进：够顺滑，又不至于每帧都跑一次调度
    private const long StepMs = 32;

    /// <summary>
    /// 数值补间（浮点版）。用 schedule 驱动，立即回调一次起始值，
    /// 结束时一定回调到 to。返回句柄，调用方可 Pause。
    /// 与 Time.timeScale 无关，游戏暂停时也会跑完。
    /// </summary>
    public static IVisualElementScheduledItem NumberTo(
        VisualElement el, float from, float to, long ms, Action<float> onChanged)
    {
        if (el == null || onChanged == null) return null;

        float step = StepMs / Mathf.Max(1f, ms);
        float t = 0f;
        onChanged(from);

        return el.schedule.Execute(() =>
        {
            t += step;
            float p = Mathf.Clamp01(t);
            onChanged(Mathf.Lerp(from, to, p));
        }).Every(StepMs).Until(() => t >= 1f);
    }

    /// <summary>数值补间（整数版）：按四舍五入取整后回调，适合金币、经验这类计数文本。</summary>
    public static IVisualElementScheduledItem NumberTo(
        VisualElement el, int from, int to, long ms, Action<int> onChanged)
    {
        if (onChanged == null) return null;
        return NumberTo(el, (float)from, (float)to, ms, v => onChanged(Mathf.RoundToInt(v)));
    }

    /// <summary>
    /// 弹窗弹出：从 fromScale 缩放到 1，带 EaseOutBack 回弹，同时淡入。
    /// 结束时会把 scale / opacity 复位成 StyleKeyword.Null，
    /// 否则内联样式会压掉 USS 上的 hover 缩放。
    /// </summary>
    public static IVisualElementScheduledItem PopIn(VisualElement el, float fromScale = 0.9f, long ms = 260)
    {
        if (el == null) return null;

        float step = StepMs / Mathf.Max(1f, ms);
        float t = 0f;

        el.style.opacity = 0f;
        el.style.scale = new StyleScale(new Scale(new Vector3(fromScale, fromScale, 1f)));

        return el.schedule.Execute(() =>
        {
            t += step;
            float p = Mathf.Clamp01(t);
            float s = Mathf.Lerp(fromScale, 1f, EaseOutBack(p));

            el.style.opacity = p;
            el.style.scale = new StyleScale(new Scale(new Vector3(s, s, 1f)));

            // 收尾撤掉内联样式，交还给 USS
            if (p >= 1f) ResetTransform(el);
        }).Every(StepMs).Until(() => t >= 1f);
    }

    /// <summary>
    /// 淡出 oldRoot 再淡入 newRoot，用于页面切换。
    /// 注意：两端结束时都会复位 opacity，所以不会破坏各自 USS 上的过渡。
    /// 调用方负责页面的 SetActive 时机——通常在切页之后再调这个方法做视觉过渡。
    /// </summary>
    public static IVisualElementScheduledItem CrossFadePages(
        VisualElement oldRoot, VisualElement newRoot, long ms = 240)
    {
        // 淡出 / 淡入各占一半，最短一个步进，避免 half 变成 0
        long half = ms / 2 < StepMs ? StepMs : ms / 2;

        return Fade(oldRoot, 1f, 0f, half, () =>
        {
            ResetOpacity(oldRoot);
            Fade(newRoot, 0f, 1f, half, () => ResetOpacity(newRoot));
        });
    }

    /// <summary>透明度补间。结束时复位 opacity 为 StyleKeyword.Null。</summary>
    public static IVisualElementScheduledItem Fade(
        VisualElement el, float from, float to, long ms, Action onDone = null)
    {
        if (el == null) return null;

        float step = StepMs / Mathf.Max(1f, ms);
        float t = 0f;
        el.style.opacity = from;

        return el.schedule.Execute(() =>
        {
            t += step;
            float p = Mathf.Clamp01(t);
            el.style.opacity = Mathf.Lerp(from, to, p);

            if (p >= 1f)
            {
                ResetOpacity(el);
                onDone?.Invoke();
            }
        }).Every(StepMs).Until(() => t >= 1f);
    }

    /// <summary>
    /// 通用抖动：随机偏移 + 线性衰减。
    /// 结束时会把 translate 复位成 StyleKeyword.Null，
    /// 否则内联 translate 会压掉 .lift > * 的抬起量，投影就没了。
    /// </summary>
    public static IVisualElementScheduledItem Shake(VisualElement el, float px = 6f, long ms = 280)
    {
        if (el == null) return null;

        float step = StepMs / Mathf.Max(1f, ms);
        float t = 0f;

        return el.schedule.Execute(() =>
        {
            t += step;
            float p = Mathf.Clamp01(t);
            float falloff = 1f - p;
            float x = UnityEngine.Random.Range(-px, px) * falloff;
            float y = UnityEngine.Random.Range(-px, px) * falloff;
            el.style.translate = new Translate(x, y);

            if (p >= 1f) ResetTranslate(el);
        }).Every(StepMs).Until(() => t >= 1f);
    }

    /// <summary>撤掉内联 opacity，把控制权交还给 USS。</summary>
    public static void ResetOpacity(VisualElement el)
    {
        if (el != null) el.style.opacity = new StyleFloat(StyleKeyword.Null);
    }

    /// <summary>撤掉内联 translate（.lift 的抬起量、hover 位移都依赖 USS 的值）。</summary>
    public static void ResetTranslate(VisualElement el)
    {
        if (el != null) el.style.translate = new StyleTranslate(StyleKeyword.Null);
    }

    /// <summary>撤掉内联 scale + opacity（PopIn 收尾用）。</summary>
    public static void ResetTransform(VisualElement el)
    {
        if (el == null) return;
        el.style.scale = new StyleScale(StyleKeyword.Null);
        el.style.opacity = new StyleFloat(StyleKeyword.Null);
    }

    /// <summary>EaseOutBack：末尾轻微过冲再回弹，弹窗弹出的手感靠它。</summary>
    private static float EaseOutBack(float p)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float f = p - 1f;
        return 1f + c3 * f * f * f + c1 * f * f;
    }
}
