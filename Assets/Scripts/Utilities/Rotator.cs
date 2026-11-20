
using UnityEngine;
public class Rotator : MonoBehaviour
{
    [SerializeField] private float rotationSpeed;

    /// <summary>
    /// 每帧按设定速度旋转自身。
    /// </summary>
    void Update()
    {
        transform.RotateAround(transform.position, Vector3.back, Time.deltaTime*100*rotationSpeed);
    }
}
