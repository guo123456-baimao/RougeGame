using System;
using System.Collections.Generic;
/// <summary>
/// 玩家档案数据模型（纯数据，不含任何 Unity API）。
/// 各 IDataStore 实现负责把它与真实存储（PlayerPrefs / 数据库）同步。
/// </summary>
[Serializable]
public class PlayerProfile
{
    public int coins;

    /// <summary>已拥有的角色 uid 列表（对应 CharacterBlueprint.uid）。</summary>
    public List<string> ownedCharacters = new List<string>();

    /// <summary>已解锁的铭文 id 列表（对应 EngravingBlueprint.engravingId）。</summary>
    public List<string> unlockedEngravings = new List<string>();

    /// <summary>当前装备的铭文 id 列表（最多 MaxSelectedSlots 个）。</summary>
    public List<string> selectedEngravings = new List<string>();

    /// <summary>
    /// 未结算的本局进度快照（RunState 序列化后的 JSON 串），空串表示"当前没有未结算的本局"。
    /// 详见 IDataStore.GetSessionSnapshot。
    /// </summary>
    public string sessionSnapshot = string.Empty;
}
