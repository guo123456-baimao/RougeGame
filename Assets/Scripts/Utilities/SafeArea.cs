using UnityEngine;
public class SafeArea : MonoBehaviour
{
    /// <summary>
    /// 按设备安全区设置界面边距。
    /// </summary>
    void Start()
    {
        ResetSafeArea();
    }

    /// <summary>
    /// 重新计算并应用安全区边距。
    /// </summary>
    public void ResetSafeArea()
    {
        RectTransform rectTransform = GetComponent<RectTransform>();
        Rect safeArea = Screen.safeArea;
        Vector2 minAnchor = safeArea.position;
        Vector2 maxAnchor = minAnchor + safeArea.size;
        
        minAnchor.x /= Screen.width;
        minAnchor.y /= Screen.height;
        maxAnchor.x /= Screen.width;
        maxAnchor.y /= Screen.height;

        rectTransform.anchorMin = minAnchor;
        rectTransform.anchorMax = maxAnchor;
    }
}
