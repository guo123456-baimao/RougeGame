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
/// 云存档写入接口。accountId 一律从 JWT 解析，客户端改不了别人的存档。
/// 金币目前由客户端上报最终值（P3），P4 会改成服务端事务扣减（真正防作弊）。
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/save")]
public class CloudSaveController : ControllerBase
{
    private const int MaxEngravingSlots = 3;
    private const int MaxCoins = 2_000_000_000;

    /// <summary>本局快照是客户端给的 JSON，必须限长，防止把 TEXT 列当垃圾桶。</summary>
    private const int MaxSessionSnapshotChars = 4_000;

    private readonly AppDbContext _db;

    public CloudSaveController(AppDbContext db) => _db = db;

    /// <summary>写回金币余额。</summary>
    [HttpPost("coins")]
    public async Task<ActionResult<ApiResult<CoinsResult>>> UpdateCoins([FromBody] CoinsUpdateRequest request)
    {
        if (request == null)
            return BadRequest(ApiResult<CoinsResult>.Fail(ApiCode.BadRequest, "请求体为空"));

        int accountId = GetAccountId();
        if (accountId <= 0)
            return Unauthorized(ApiResult<CoinsResult>.Fail(ApiCode.Unauthorized, "token 无效"));

        int coins = Math.Clamp(request.Coins, 0, MaxCoins);

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
        if (account == null)
            return Unauthorized(ApiResult<CoinsResult>.Fail(ApiCode.Unauthorized, "账号不存在"));

        account.Coins = coins;
        await _db.SaveChangesAsync();

        return Ok(ApiResult<CoinsResult>.Success(new CoinsResult(account.Coins)));
    }

    /// <summary>写回角色拥有标记（存在则更新，不存在则插入）。</summary>
    [HttpPost("character-owned")]
    public async Task<ActionResult<ApiResult<bool>>> UpdateCharacterOwned([FromBody] CharacterOwnedRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.CharacterUid))
            return BadRequest(ApiResult<bool>.Fail(ApiCode.BadRequest, "characterUid 不能为空"));

        int accountId = GetAccountId();
        if (accountId <= 0)
            return Unauthorized(ApiResult<bool>.Fail(ApiCode.Unauthorized, "token 无效"));

        var row = await _db.CharacterOwnerships.FirstOrDefaultAsync(
            x => x.AccountId == accountId && x.CharacterUid == request.CharacterUid);

        if (row == null)
        {
            _db.CharacterOwnerships.Add(new CharacterOwnership
            {
                AccountId = accountId,
                CharacterUid = request.CharacterUid,
                Owned = request.Owned,
                AcquiredAt = DateTime.UtcNow
            });
        }
        else
        {
            row.Owned = request.Owned;
            if (request.Owned) row.AcquiredAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok(ApiResult<bool>.Success(request.Owned));
    }

    /// <summary>
    /// 写入未结算的本局进度快照（本局已购商品名 / 未发放的加成 / 待投放消耗品）。
    /// 客户端传空串即清空——一局结束（死亡 / 通关 / 返回首页）时就是这么调的。
    /// </summary>
    [HttpPost("session")]
    public async Task<ActionResult<ApiResult<bool>>> UpdateSession([FromBody] SessionSnapshotRequest request)
    {
        int accountId = GetAccountId();
        if (accountId <= 0)
            return Unauthorized(ApiResult<bool>.Fail(ApiCode.Unauthorized, "token 无效"));

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
        if (account == null)
            return Unauthorized(ApiResult<bool>.Fail(ApiCode.Unauthorized, "账号不存在"));

        string? snapshot = request?.Snapshot;
        if (!string.IsNullOrEmpty(snapshot) && snapshot!.Length > MaxSessionSnapshotChars)
            snapshot = snapshot.Substring(0, MaxSessionSnapshotChars);

        account.PendingSession = string.IsNullOrEmpty(snapshot) ? null : snapshot;
        await _db.SaveChangesAsync();

        return Ok(ApiResult<bool>.Success(true));
    }

    /// <summary>解锁铭文（幂等，重复解锁不会产生脏数据）。</summary>
    [HttpPost("engraving-unlock")]
    public async Task<ActionResult<ApiResult<bool>>> UnlockEngraving([FromBody] EngravingUnlockRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.EngravingId))
            return BadRequest(ApiResult<bool>.Fail(ApiCode.BadRequest, "engravingId 不能为空"));

        int accountId = GetAccountId();
        if (accountId <= 0)
            return Unauthorized(ApiResult<bool>.Fail(ApiCode.Unauthorized, "token 无效"));

        bool exists = await _db.EngravingsUnlocked.AnyAsync(
            x => x.AccountId == accountId && x.EngravingId == request.EngravingId);

        if (!exists)
        {
            _db.EngravingsUnlocked.Add(new EngravingUnlocked
            {
                AccountId = accountId,
                EngravingId = request.EngravingId,
                UnlockedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        return Ok(ApiResult<bool>.Success(true));
    }

    /// <summary>
    /// 整体覆写装备中的铭文。先删后插放在一个事务里，
    /// 避免中途失败留下半新半旧的装备列表。
    /// </summary>
    [HttpPost("engraving-select")]
    public async Task<ActionResult<ApiResult<bool>>> SelectEngravings([FromBody] EngravingSelectRequest request)
    {
        if (request == null || request.EngravingIds == null)
            return BadRequest(ApiResult<bool>.Fail(ApiCode.BadRequest, "请求体为空"));

        int accountId = GetAccountId();
        if (accountId <= 0)
            return Unauthorized(ApiResult<bool>.Fail(ApiCode.Unauthorized, "token 无效"));

        // 去重、去空、截断到槽位上限——业务规则在服务端兜底，客户端传什么都伤不到数据
        var ids = request.EngravingIds
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .Take(MaxEngravingSlots)
            .ToList();

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var existing = await _db.EngravingsSelected
                .Where(e => e.AccountId == accountId)
                .ToListAsync();
            _db.EngravingsSelected.RemoveRange(existing);
            await _db.SaveChangesAsync();

            byte slot = 0;
            foreach (var id in ids)
            {
                _db.EngravingsSelected.Add(new EngravingSelected
                {
                    AccountId = accountId,
                    EngravingId = id,
                    SlotIndex = slot++
                });
            }
            await _db.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return Ok(ApiResult<bool>.Success(true));
    }

    private int GetAccountId()
    {
        string? claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out int id) ? id : 0;
    }
}
