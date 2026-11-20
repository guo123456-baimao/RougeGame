using System.Collections;
using UnityEngine;
using TMPro;
public class DamageText : MonoBehaviour
{
    private EntityManager entityManager;
    private TextMeshPro text;

    /// <summary>
    /// 缓存 TMP 文本组件。
    /// </summary>
    void Awake()
    {
        text = GetComponent<TextMeshPro>();
    }

    /// <summary>
    /// 注入实体管理器。
    /// </summary>
    public void Init(EntityManager entityManager)
    {
        this.entityManager = entityManager;
    }

    /// <summary>
    /// 设置位置与伤害数值，并重新播放飘字动画。
    /// </summary>
    public void Setup(Vector2 position, float damage)
    {
        transform.position = position;
        text.text = damage.ToString("N0");
        StopAllCoroutines();
        StartCoroutine(AnimateText());
    }

    /// <summary>
    /// 边上升边以回弹缓动缩小，动画结束后回收飘字。
    /// </summary>
    IEnumerator AnimateText()
    {
        float t = 0;
        while (t < 1)
        {
            transform.position += Vector3.up * Time.deltaTime * 0.5f;
            transform.localScale = Vector3.one * EasingUtils.EaseOutBack(1-t);
            yield return null;
            t += Time.deltaTime*2;
        }
        entityManager.DespawnDamageText(this);
    }
}
