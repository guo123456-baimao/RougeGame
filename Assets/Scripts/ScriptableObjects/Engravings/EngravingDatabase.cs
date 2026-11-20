using UnityEngine;
/// <summary>
/// 全局铭文库。铭文结算不再依赖每个 CharacterBlueprint 各自挂一份数组，
/// 统一从这里取，避免角色蓝图漏挂导致装备了铭文却不生效。
/// </summary>
[CreateAssetMenu(fileName = "EngravingDatabase", menuName = "Blueprints/Engraving Database")]
public class EngravingDatabase : ScriptableObject
{
    public EngravingBlueprint[] allEngravings = new EngravingBlueprint[0];
}
