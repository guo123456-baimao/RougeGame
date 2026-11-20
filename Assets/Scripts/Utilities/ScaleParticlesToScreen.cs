using UnityEngine;
public class ScaleParticlesToScreen : MonoBehaviour
{
    /// <summary>
    /// 按屏幕的世界尺寸设置粒子系统缩放。
    /// </summary>
    void Awake()
    {
        ParticleSystem particleSystem = GetComponent<ParticleSystem>();
        // 计算屏幕在世界空间中的尺寸，以便正确设置粒子系统的缩放
        Vector2 bottomLeft = Camera.main.ViewportToWorldPoint(new Vector3(0, 0, Camera.main.nearClipPlane));
        Vector2 topRight = Camera.main.ViewportToWorldPoint(new Vector3(1, 1, Camera.main.nearClipPlane));
        Vector3 screenSizeWorldSpace = new Vector3(topRight.x - bottomLeft.x, topRight.y - bottomLeft.y, 1);
        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.radius = screenSizeWorldSpace.x/2;
        transform.localPosition = Vector3.up * screenSizeWorldSpace.y/2;
    }
}
