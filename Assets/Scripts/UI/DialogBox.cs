using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class DialogBox : MonoBehaviour
{
    [SerializeField] private bool appearInstantly = false;
    [SerializeField] private float animationSpeed;
    [Header("Juice")]
    [Tooltip("可选的暗底，打开时淡入、关闭时淡出")]
    [SerializeField] protected Graphic backdrop;
    [SerializeField] protected float backdropFadeDuration = 0.18f;
    [Tooltip("关闭时缩回去再隐藏，不要啪一下消失")]
    [SerializeField] protected bool animateClose = true;
    [SerializeField] protected float closeAnimationSpeed = 6f;

    private float backdropTargetAlpha = -1f;

    /// <summary>
    /// 打开对话框：激活对象、重置状态并播放打开动画。
    /// </summary>
    public virtual void Open()
    {
        gameObject.SetActive(true);
        StopAllCoroutines();
        if (appearInstantly)
        {
            transform.localScale = Vector3.one;
        }
        else
        {
            StartCoroutine(OpenAnimation());
        }
        if (backdrop != null)
        {
            if (backdropTargetAlpha < 0f)
                backdropTargetAlpha = backdrop.color.a;
            StartCoroutine(FadeBackdrop(backdropTargetAlpha));
        }
    }

    /// <summary>
    /// 关闭对话框并播放关闭动画。
    /// </summary>
    public virtual void Close()
    {
        StopAllCoroutines();
        if (animateClose && gameObject.activeInHierarchy)
        {
            StartCoroutine(CloseAnimation());
            if (backdrop != null)
                StartCoroutine(FadeBackdrop(0f));
        }
        else
        {
            transform.localScale = Vector3.zero;
            gameObject.SetActive(false);
            if (backdrop != null)
                SetBackdropAlpha(0f);
        }
    }

    /// <summary>
    /// 「继续」按钮：推进对话框到下一步。
    /// </summary>
    public virtual void Continue()
    {
        AudioManager.Instance.PlayClick();
        Close();
    }

    /// <summary>
    /// 「返回」按钮：回到上一步或关闭对话框。
    /// </summary>
    public virtual void Return()
    {
        AudioManager.Instance.PlayClick();
        Close();
    }

    /// <summary>
    /// 打开动画：弹入对话框并淡入背景遮罩。
    /// </summary>
    private IEnumerator OpenAnimation()
    {
        float t = 0;
        while (t < 1)
        {
            transform.localScale = Vector3.LerpUnclamped(Vector3.zero, Vector3.one, EasingUtils.EaseOutBack(t));
            t += Time.unscaledDeltaTime * animationSpeed;
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 关闭动画：淡出对话框与背景遮罩。
    /// </summary>
    private IEnumerator CloseAnimation()
    {
        float t = 1f;
        while (t > 0f)
        {
            t -= Time.unscaledDeltaTime * closeAnimationSpeed;
            transform.localScale = Vector3.one * EasingUtils.EaseInQuad(Mathf.Clamp01(t));
            yield return null;
        }
        transform.localScale = Vector3.zero;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 把背景遮罩渐变到目标透明度。
    /// </summary>
    private IEnumerator FadeBackdrop(float target)
    {
        if (backdrop == null) yield break;

        float from = backdrop.color.a;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, backdropFadeDuration);
            SetBackdropAlpha(Mathf.Lerp(from, target, t));
            yield return null;
        }
        SetBackdropAlpha(target);
    }

    /// <summary>
    /// 设置背景遮罩的透明度。
    /// </summary>
    private void SetBackdropAlpha(float alpha)
    {
        if (backdrop == null) return;
        Color color = backdrop.color;
        color.a = alpha;
        backdrop.color = color;
    }
}
