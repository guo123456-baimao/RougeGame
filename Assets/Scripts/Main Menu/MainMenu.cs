using UnityEngine;
/// <summary>
/// 首页枢纽：负责主菜单 BGM 与两个 UITK 页面（角色页 / 强化页）的入口调度。
/// 旧 uGUI 版页面已删除，所有入口只走 UITK。
/// </summary>
public class MainMenu : MonoBehaviour
{
    [SerializeField] private CharacterPageUITKController characterUITKPage;
    [SerializeField] private EngravingPageUITKController engravingUITKPage;

    /// <summary>
    /// 进入主菜单时播放背景音乐并刷新界面上显示的金币数。
    /// </summary>
    private void Start()
    {
        AudioManager.Instance.PlayBGMMain();
        CoinDisplayManager.UpdateAll();
    }

    /// <summary>首页「开始」按钮的入口：打开 UITK 角色页。</summary>
    public void OpenCharacterPage()
    {
        if (characterUITKPage != null)
        {
            characterUITKPage.Open();
            return;
        }

        Debug.LogWarning("[MainMenu] 没有挂 CharacterPageUITKController，「开始」入口无响应。");
    }

    /// <summary>首页「强化」入口：打开 UITK 强化页。</summary>
    public void OpenEngravingPage()
    {
        AudioManager.Instance.PlayClick();
        if (engravingUITKPage != null)
        {
            engravingUITKPage.Open();
            return;
        }

        Debug.LogWarning("[MainMenu] 没有挂 EngravingPageUITKController，「强化」入口无响应。");
    }
}
