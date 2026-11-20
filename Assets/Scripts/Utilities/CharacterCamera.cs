using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class CharacterCamera : MonoBehaviour
{
    [SerializeField] private float width = 6.0f;
    private Camera characterCamera;
    
    /// <summary>
    /// 初始化摄像机跟随目标与偏移。
    /// </summary>
    void Awake()
    {
        characterCamera = GetComponent<Camera>();
        characterCamera.orthographicSize = width/characterCamera.aspect;
    }
}
