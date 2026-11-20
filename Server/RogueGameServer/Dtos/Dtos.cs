namespace RogueGameServer.Dtos;

public record RegisterRequest(string Username, string Password);
public record LoginRequest(string Username, string Password);

/// <summary>注册/登录成功后返回的内容，客户端据此保存 token。</summary>
public record AuthData(long AccountId, string Username, string AccessToken, long ExpiresIn);

public record CharacterOwnedDto(string Uid, bool Owned);

public record EngravingProgressDto(List<string> Unlocked, List<string> Selected);

/// <summary>完整玩家档案，对应 GET /api/v1/profile。</summary>
public record ProfileDto(
    long AccountId,
    string Username,
    int Coins,
    List<CharacterOwnedDto> Characters,
    EngravingProgressDto Engravings,
    /// <summary>未结算的本局进度快照（JSON），没有则为空串。</summary>
    string SessionSnapshot);
