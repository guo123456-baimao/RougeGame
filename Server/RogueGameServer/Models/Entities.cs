using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RogueGameServer.Models;

/// <summary>账号表：对应 MySQL rogue_game.accounts（含金币余额）。</summary>
[Table("accounts")]
public class Account
{
    [Key] [Column("id")] public long Id { get; set; }

    [MaxLength(32)] [Column("username")] public string Username { get; set; } = string.Empty;

    /// <summary>PBKDF2 结果，64 字节。</summary>
    [MaxLength(64)] [Column("password_hash")] public byte[] PasswordHash { get; set; } = Array.Empty<byte>();

    /// <summary>每账号独立随机盐，16 字节。</summary>
    [MaxLength(16)] [Column("salt")] public byte[] Salt { get; set; } = Array.Empty<byte>();

    [Column("coins")] public int Coins { get; set; }

    /// <summary>1 = 正常，0 = 封禁。</summary>
    [Column("status")] public byte Status { get; set; } = 1;

    [Column("created_at")] public DateTime CreatedAt { get; set; }

    [Column("last_login_at")] public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// 未结算的本局进度快照（RunState 序列化后的 JSON）。
    /// 金币已经从账号里扣了，买到的东西就必须记在账号上，
    /// 否则玩家退出登录再回来"花了钱什么都没有"。一局结束后由客户端传空串清空。
    /// </summary>
    [Column("pending_session")] public string? PendingSession { get; set; }

    public ICollection<CharacterOwnership> Characters { get; set; } = new List<CharacterOwnership>();
    public ICollection<EngravingUnlocked> EngravingsUnlocked { get; set; } = new List<EngravingUnlocked>();
    public ICollection<EngravingSelected> EngravingsSelected { get; set; } = new List<EngravingSelected>();
}

/// <summary>角色拥有关系：对应 character_ownership，复合主键 (account_id, character_uid)。</summary>
[Table("character_ownership")]
public class CharacterOwnership
{
    [Column("account_id")] public long AccountId { get; set; }
    [MaxLength(64)] [Column("character_uid")] public string CharacterUid { get; set; } = string.Empty;
    [Column("owned")] public bool Owned { get; set; }
    [Column("acquired_at")] public DateTime AcquiredAt { get; set; }

    public Account? Account { get; set; }
}

/// <summary>已解锁铭文：对应 engraving_unlocked，复合主键 (account_id, engraving_id)。</summary>
[Table("engraving_unlocked")]
public class EngravingUnlocked
{
    [Column("account_id")] public long AccountId { get; set; }
    [MaxLength(64)] [Column("engraving_id")] public string EngravingId { get; set; } = string.Empty;
    [Column("unlocked_at")] public DateTime UnlockedAt { get; set; }

    public Account? Account { get; set; }
}

/// <summary>已装备铭文（最多 3 个）：对应 engraving_selected，uk_eng_slot 在数据库层强制槽位唯一。</summary>
[Table("engraving_selected")]
public class EngravingSelected
{
    [Column("account_id")] public long AccountId { get; set; }
    [MaxLength(64)] [Column("engraving_id")] public string EngravingId { get; set; } = string.Empty;
    [Column("slot_index")] public byte SlotIndex { get; set; }

    public Account? Account { get; set; }
}
