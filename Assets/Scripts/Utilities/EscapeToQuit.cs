using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class EscapeToQuit : MonoBehaviour
{
    /// <summary>
    /// 缓存退出检测所需的状态。
    /// </summary>
    void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }

    /// <summary>
    /// 按下退出键时关闭游戏。
    /// </summary>
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
    }
}
