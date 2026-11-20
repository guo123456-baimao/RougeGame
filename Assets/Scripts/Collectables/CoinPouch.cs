using UnityEngine;
public class CoinPouch : MonoBehaviour
{
    private Character playerCharacter;

    /// <summary>
    /// 注入玩家引用。
    /// </summary>
    public void Init(Character playerCharacter)
    {
        this.playerCharacter = playerCharacter;
    }

    
}
