namespace RogueGameServer.Services;

/// <summary>
/// 登录暴力破解防护：同一用户名连续失败 MaxAttempts 次后锁定 LockSeconds 秒。
/// 单实例内存计数即可（生产环境多实例应换成 Redis）。
/// </summary>
public class LoginThrottle
{
    private const int MaxAttempts = 5;
    private const int LockSeconds = 60;

    private readonly Dictionary<string, (int Count, DateTime LockedUntil)> _attempts = new();
    private readonly object _gate = new();

    public bool IsLocked(string username, out string message)
    {
        lock (_gate)
        {
            message = string.Empty;
            if (!_attempts.TryGetValue(username, out var state)) return false;
            if (state.LockedUntil <= DateTime.UtcNow) return false;

            int remain = (int)Math.Ceiling((state.LockedUntil - DateTime.UtcNow).TotalSeconds);
            message = $"登录失败次数过多，请 {remain} 秒后重试";
            return true;
        }
    }

    public void RecordFailure(string username)
    {
        lock (_gate)
        {
            if (!_attempts.TryGetValue(username, out var state))
                state = (0, DateTime.MinValue);

            int count = state.Count + 1;
            DateTime lockedUntil = count >= MaxAttempts ? DateTime.UtcNow.AddSeconds(LockSeconds) : state.LockedUntil;
            _attempts[username] = (count, lockedUntil);
        }
    }

    public void Reset(string username)
    {
        lock (_gate) _attempts.Remove(username);
    }
}
