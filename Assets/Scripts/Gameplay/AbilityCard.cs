using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class AbilityCard : MonoBehaviour
{
    [SerializeField] private Image abilityImage;
    [SerializeField] private RectTransform abilityImageRect;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI buttonText;
    [SerializeField] private float appearSpeed = 3;
    private AbilitySelectionDialog levelUpMenu;
    private Ability ability;
    private bool initialized;
    private Vector3 defaultScale;

    /// <summary>
    /// 缓存卡片初始缩放，供出现动画还原使用。
    /// </summary>
    private void Awake()
    {
        defaultScale = transform.localScale;
    }

    /// <summary>
    /// 按技能数据刷新卡片的名称、描述与「选择/升级」按钮文案。
    /// </summary>
    private void SetText()
    {
        if (!initialized) return;

        nameText.text = ability.Name;
        descriptionText.text = ability.Description;

        buttonText.text = !ability.Owned ? "选择" : ("升级" + " (" + ability.Level + " -> " + (ability.Level+1) + ")");
    }

    /// <summary>
    /// 绑定所属选卡界面与技能，按立绘比例调整图片尺寸并播放出现动画。
    /// </summary>
    public void Init(AbilitySelectionDialog levelUpMenu, Ability ability, float waitToAppear)
    {
        abilityImage.sprite = ability.Image;
        float yHeight = abilityImageRect.rect.height;
        float xWidth = ability.Image.textureRect.width / (float) ability.Image.textureRect.height * yHeight;
        if (xWidth > abilityImageRect.rect.width)
        {
                xWidth = abilityImageRect.rect.width;
                yHeight = ability.Image.textureRect.height / (float) ability.Image.textureRect.width * xWidth;
        }
        ((RectTransform)abilityImage.transform).sizeDelta = new Vector2(xWidth, yHeight);
        this.levelUpMenu = levelUpMenu;
        this.ability = ability;
        StartCoroutine(Appear(waitToAppear));
        initialized = true;
        SetText();
    }

    /// <summary>
    /// 延迟后以回弹缓动把卡片从 0 缩放到原始大小。
    /// </summary>
    public IEnumerator Appear(float waitToAppear)
    {
        Vector3 initialScale = defaultScale;
        transform.localScale = Vector3.zero;
        yield return new WaitForSecondsRealtime(waitToAppear);
        float t = 0;
        while (t < 1)
        {
            transform.localScale = Vector3.LerpUnclamped(Vector3.zero, initialScale, EasingUtils.EaseOutBack(t));
            t += Time.unscaledDeltaTime * appearSpeed;
            yield return null;
        }
        transform.localScale = initialScale;
    }

    /// <summary>
    /// 选中该卡片：应用技能、关闭选卡界面并播放点击音效。
    /// </summary>
    public void Selected()
    {
        ability.Select();
        levelUpMenu.Close();
        AudioManager.Instance.PlayClick();
    }
}
