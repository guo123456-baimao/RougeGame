using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class ZPositioner : MonoBehaviour
{
    private Transform playerTransform;
    private float scale = 0.01f;
    private bool manuallySetZ = false;
    private float manualY;

    /// <summary>
    /// 记录用于计算排序基准的 Transform。
    /// </summary>
    public void Init(Transform playerTransform)
    {
        this.playerTransform = playerTransform;
    }

    /// <summary>
    /// 按 Y 坐标自动刷新 Z 值。
    /// </summary>
    void LateUpdate()
    {
        if (playerTransform == null) return;  // 异步初始化完成前不参与深度排序
        Vector3 temp = transform.position;
        temp.z = scale*((manuallySetZ ? manualY : temp.y)-playerTransform.position.y);
        transform.position = temp;
    }

    /// <summary>
    /// 按给定的 Y 坐标计算并设置 Z 值。
    /// </summary>
    public void ManuallySetZByY(float y)
    {
        manuallySetZ = true;
        manualY = y;
    }

    /// <summary>
    /// 按自身当前 Y 位置设置 Z 值。
    /// </summary>
    public void AutomaticallySetZ()
    {
        manuallySetZ = false;
    }
}
