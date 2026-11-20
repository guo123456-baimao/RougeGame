namespace RogueGameServer.Dtos;

/// <summary>
/// 一次消费的"订单"。P4 之前客户端直接上报金币最终值，改本地文件就能刷币；
/// 现在所有扣费都走这一份订单，由服务端在同一事务里完成：校验余额 → 扣费 → 写权益。
/// </summary>
// 后三个字段可选：必须声明成可空引用类型，否则 ApiController 会把客户端传来的
// null 当成校验失败，直接返回 400（Unity 侧未解锁角色/铭文的订单正是传 null）
public record SpendRequest(
    string ItemId,
    int Cost,
    string? GrantsCharacterUid,
    string? UnlocksEngraving,
    int GrantsCoins);

/// <summary>扣费结果，coins 是服务端算出来的权威余额，客户端直接用它校正本地缓存。</summary>
public record SpendResult(
    int Coins,
    bool GrantedCharacter,
    bool UnlockedEngraving);

/// <summary>单局结算奖励。</summary>
public record SettleRunRequest(int CoinsGained);
