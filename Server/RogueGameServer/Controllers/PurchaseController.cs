using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RogueGameServer.Common;
using RogueGameServer.Data;
using RogueGameServer.Dtos;
using RogueGameServer.Models;

namespace RogueGameServer.Controllers;

/// <summary>
/// 消费与结算接口。
/// 核心是 SELECT ... FOR UPDATE 行锁 + 事务，
/// 保证"余额够就一定能买成，两个请求同时扣也不会把金币扣穿"。
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1")]
public class PurchaseController : ControllerBase
{
    /// <summary>单局最多能带出多少金币，防止客户端谎报战果。</summary>
    private const int MaxRunReward = 1_000_000;

    private readonly AppDbContext _db;

    public PurchaseController(AppDbContext db) => _db = db;

    /// <summary>
    /// 原子扣费：余额校验、扣币、写永久权益要么全成功要么全失败。
    /// </summary>
    [HttpPost("shop/purchase")]
    public async Task<ActionResult<ApiResult<SpendResult>>> Purchase([FromBody] SpendRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ItemId))
            return BadRequest(ApiResult<SpendResult>.Fail(ApiCode.BadRequest, "itemId 不能为空"));

        if (request.Cost < 0 || request.GrantsCoins < 0)
            return BadRequest(ApiResult<SpendResult>.Fail(ApiCode.BadRequest, "金额不能为负"));

        int accountId = GetAccountId();
        if (accountId <= 0)
            return Unauthorized(ApiResult<SpendResult>.Fail(ApiCode.Unauthorized, "token 无效"));

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            // 行锁：这条记录在自己提交前，其他事务读不到新值也不会重复扣
            var account = await _db.Accounts
                .FromSqlInterpolated($"SELECT * FROM accounts WHERE id = {accountId} FOR UPDATE")
                .FirstOrDefaultAsync();

            if (account == null)
            {
                await transaction.RollbackAsync();
                return Unauthorized(ApiResult<SpendResult>.Fail(ApiCode.Unauthorized, "账号不存在"));
            }

            int balance = account.Coins - request.Cost + request.GrantsCoins;
            if (balance < 0)
            {
                await transaction.RollbackAsync();
                return Ok(ApiResult<SpendResult>.Fail(ApiCode.NotEnoughCoins, "金币不足"));
            }

            account.Coins = balance;

            bool grantedCharacter = false;
            bool unlockedEngraving = false;

            if (!string.IsNullOrWhiteSpace(request.GrantsCharacterUid))
            {
                await UpsertCharacterOwnership(accountId, request.GrantsCharacterUid, true);
                grantedCharacter = true;
            }

            if (!string.IsNullOrWhiteSpace(request.UnlocksEngraving))
            {
                bool exists = await _db.EngravingsUnlocked.AnyAsync(
                    x => x.AccountId == accountId && x.EngravingId == request.UnlocksEngraving);
                if (!exists)
                {
                    _db.EngravingsUnlocked.Add(new EngravingUnlocked
                    {
                        AccountId = accountId,
                        EngravingId = request.UnlocksEngraving,
                        UnlockedAt = DateTime.UtcNow
                    });
                }
                unlockedEngraving = true;
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            var result = new SpendResult(account.Coins, grantedCharacter, unlockedEngraving);
            return Ok(ApiResult<SpendResult>.Success(result));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>单局结束结算奖励，服务端加币（客户端说多少不算数，先过上限）。</summary>
    [HttpPost("run/settle")]
    public async Task<ActionResult<ApiResult<CoinsResult>>> SettleRun([FromBody] SettleRunRequest request)
    {
        if (request == null || request.CoinsGained < 0)
            return BadRequest(ApiResult<CoinsResult>.Fail(ApiCode.BadRequest, "结算金额非法"));

        int accountId = GetAccountId();
        if (accountId <= 0)
            return Unauthorized(ApiResult<CoinsResult>.Fail(ApiCode.Unauthorized, "token 无效"));

        int gained = Math.Min(request.CoinsGained, MaxRunReward);

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            var account = await _db.Accounts
                .FromSqlInterpolated($"SELECT * FROM accounts WHERE id = {accountId} FOR UPDATE")
                .FirstOrDefaultAsync();

            if (account == null)
            {
                await transaction.RollbackAsync();
                return Unauthorized(ApiResult<CoinsResult>.Fail(ApiCode.Unauthorized, "账号不存在"));
            }

            account.Coins = account.Coins + gained;
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(ApiResult<CoinsResult>.Success(new CoinsResult(account.Coins)));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task UpsertCharacterOwnership(int accountId, string uid, bool owned)
    {
        var row = await _db.CharacterOwnerships.FirstOrDefaultAsync(
            x => x.AccountId == accountId && x.CharacterUid == uid);

        if (row == null)
        {
            _db.CharacterOwnerships.Add(new CharacterOwnership
            {
                AccountId = accountId,
                CharacterUid = uid,
                Owned = owned,
                AcquiredAt = DateTime.UtcNow
            });
        }
        else
        {
            row.Owned = owned;
            if (owned) row.AcquiredAt = DateTime.UtcNow;
        }
    }

    private int GetAccountId()
    {
        string? claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out int id) ? id : 0;
    }
}
