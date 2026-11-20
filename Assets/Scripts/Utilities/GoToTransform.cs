using UnityEngine;
public class GoToTransform : MonoBehaviour
{
    [SerializeField] private Transform targetTransform;

    /// <summary>
    /// 每帧把自身位置同步到目标 Transform。
    /// </summary>
    void LateUpdate()
    {
        transform.position = targetTransform.position;
    }
}
