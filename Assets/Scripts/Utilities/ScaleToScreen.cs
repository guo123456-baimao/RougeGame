using UnityEngine;

public class ScaleToScreen : MonoBehaviour
{
    /// <summary>
    /// 把自身缩放到当前屏幕的世界尺寸。
    /// </summary>
    void Awake()
    {
        // 计算屏幕在世界空间中的尺寸，并把物体缩放到该尺寸
        Vector2 bottomLeft = Camera.main.ViewportToWorldPoint(new Vector3(0, 0, Camera.main.nearClipPlane));
        Vector2 topRight = Camera.main.ViewportToWorldPoint(new Vector3(1, 1, Camera.main.nearClipPlane));
        Vector3 screenSizeWorldSpace = new Vector3(topRight.x - bottomLeft.x, topRight.y - bottomLeft.y, 1);
        transform.localScale = screenSizeWorldSpace;
    }
}
