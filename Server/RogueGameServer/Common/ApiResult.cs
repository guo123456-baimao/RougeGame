namespace RogueGameServer.Common;

/// <summary>统一响应码（与 Docs/联网云存档改造方案.md §5.2 保持一致）。</summary>
public static class ApiCode
{
    public const int Ok = 0;
    public const int BadRequest = 1001;
    public const int Unauthorized = 1002;
    public const int TokenExpired = 1003;
    public const int NotEnoughCoins = 2001;
    public const int AlreadyPurchased = 2002;
    public const int PurchaseLimitReached = 2003;
    public const int EngravingSlotsFull = 2004;
    public const int UsernameTaken = 3001;
    public const int BadCredentials = 3002;
    public const int AccountBanned = 3003;
    public const int ServerError = 9001;
}

/// <summary>所有接口统一返回 { code, message, data }，客户端只判断 code。</summary>
public class ApiResult<T>
{
    public int Code { get; init; }
    public string Message { get; init; } = "ok";
    public T? Data { get; init; }

    public static ApiResult<T> Success(T data) => new() { Code = ApiCode.Ok, Data = data };
    public static ApiResult<T> Fail(int code, string message) => new() { Code = code, Message = message };
}
