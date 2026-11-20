#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class CharacterSetGenerator : EditorWindow
{
    [MenuItem("Tools/CharacterSetGenerator")]
    static void Open() => GetWindow<CharacterSetGenerator>("Character Set Generator");

    [SerializeField] private string _characterSetFilePath = "Assets/Fonts/CharacterSet.txt";
    [SerializeField, TextArea] private string _defaultCharacters = "1234567890-=！@#￥%……&*()~:\"{}[]|\\?/<>,.;'+abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ【】「」。：，";

    /// <summary>
    /// 提供一键生成角色立绘集合的调试按钮。
    /// </summary>
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Character Set Generator", EditorStyles.boldLabel);
        _characterSetFilePath = EditorGUILayout.TextField("Output Path", _characterSetFilePath);
        EditorGUILayout.TextArea(_defaultCharacters);

        if (GUILayout.Button("Generate Character Set"))
        {
            File.WriteAllText(_characterSetFilePath, _defaultCharacters);
            AssetDatabase.Refresh();
            Debug.Log("Character Set Generated!");
        }
    }
}

#endif
