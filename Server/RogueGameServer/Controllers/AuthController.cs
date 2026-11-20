using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RogueGameServer.Common;
using RogueGameServer.Data;
using RogueGameServer.Dtos;
using RogueGameServer.Models;
using RogueGameServer.Services;

namespace RogueGameServer.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokenService;
    private readonly LoginThrottle _throttle;

    public AuthController(AppDbContext db, TokenService tokenService, LoginThrottle throttle)
    {
        _db = db;
        _tokenService = tokenService;
        _throttle = throttle;
    }

    /// <summary>注册：用户名 3-32 位、密码 ≥6 位，密码以 PBKDF2 加盐哈希入库。</summary>
    [HttpPost("register")]
    public async Task<ActionResult<ApiResult<AuthData>>> Register([FromBody] RegisterRequest request)
    {
        if (request == null)
            return BadRequest(ApiResult<AuthData>.Fail(ApiCode.BadRequest, "请求体为空"));

        string username = request.Username?.Trim() ?? string.Empty;
        string password = request.Password ?? string.Empty;

        if (username.Length < 3 || username.Length > 32)
            return BadRequest(ApiResult<AuthData>.Fail(ApiCode.BadRequest, "用户名长度需为 3-32 位"));
        if (password.Length < 6)
            return BadRequest(ApiResult<AuthData>.Fail(ApiCode.BadRequest, "密码至少 6 位"));

        bool exists = await _db.Accounts.AnyAsync(a => a.Username == username);
        if (exists)
            return Ok(ApiResult<AuthData>.Fail(ApiCode.UsernameTaken, "用户名已存在"));

        var (hash, salt) = PasswordHasher.Hash(password);

        var account = new Account
        {
            Username = username,
            PasswordHash = hash,
            Salt = salt,
            Coins = 0,
            Status = 1,
            CreatedAt = DateTime.UtcNow
        };

        _db.Accounts.Add(account);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // 并发注册同一用户名时落在 uk_accounts_username 唯一索引上
            return Ok(ApiResult<AuthData>.Fail(ApiCode.UsernameTaken, "用户名已存在"));
        }

        var (token, expiresIn) = _tokenService.CreateToken(account.Id, account.Username);
        return Ok(ApiResult<AuthData>.Success(new AuthData(account.Id, account.Username, token, expiresIn)));
    }

    /// <summary>登录：失败 5 次锁定 1 分钟（LoginThrottle）。</summary>
    [HttpPost("login")]
    public async Task<ActionResult<ApiResult<AuthData>>> Login([FromBody] LoginRequest request)
    {
        if (request == null)
            return BadRequest(ApiResult<AuthData>.Fail(ApiCode.BadRequest, "请求体为空"));

        string username = request.Username?.Trim() ?? string.Empty;
        string password = request.Password ?? string.Empty;

        if (_throttle.IsLocked(username, out string lockMessage))
            return Ok(ApiResult<AuthData>.Fail(ApiCode.BadCredentials, lockMessage));

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Username == username);
        if (account == null || !PasswordHasher.Verify(password, account.PasswordHash, account.Salt))
        {
            _throttle.RecordFailure(username);
            return Ok(ApiResult<AuthData>.Fail(ApiCode.BadCredentials, "用户名或密码错误"));
        }

        if (account.Status == 0)
            return Ok(ApiResult<AuthData>.Fail(ApiCode.AccountBanned, "账号已被封禁"));

        _throttle.Reset(username);
        account.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var (token, expiresIn) = _tokenService.CreateToken(account.Id, account.Username);
        return Ok(ApiResult<AuthData>.Success(new AuthData(account.Id, account.Username, token, expiresIn)));
    }
}
