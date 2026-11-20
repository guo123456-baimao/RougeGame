namespace RogueGameServer.Dtos;

/// <summary>写入金币余额（客户端本地缓存的最终值）。</summary>
public record CoinsUpdateRequest(int Coins);

/// <summary>写入角色拥有标记。</summary>
public record CharacterOwnedRequest(string CharacterUid, bool Owned);

/// <summary>解锁铭文。</summary>
public record EngravingUnlockRequest(string EngravingId);

/// <summary>整体覆写装备中的铭文（最多 3 个，顺序即槽位）。</summary>
public record EngravingSelectRequest(List<string> EngravingIds);

/// <summary>写入未结算的本局进度快照（传空串表示本局已结束，清空记录）。</summary>
public record SessionSnapshotRequest(string Snapshot);

/// <summary>写入后统一返回的最新余额，客户端用它校正本地缓存。</summary>
public record CoinsResult(int Coins);
