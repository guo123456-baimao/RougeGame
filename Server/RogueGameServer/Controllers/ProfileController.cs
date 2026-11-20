using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RogueGameServer.Common;
using RogueGameServer.Data;
using RogueGameServer.Dtos;

namespace RogueGameServer.Controllers;

/// <summary>
/// 玩家档案：拉取完整云存档。
/// accountId 一律从 JWT 里解析，绝不信任请求参数，杜绝越权读他人存档。
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/profile")]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProfileController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<ApiResult<ProfileDto>>> Get()
    {
        string? idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(idClaim, out long accountId))
            return Unauthorized(ApiResult<ProfileDto>.Fail(ApiCode.Unauthorized, "token 无效"));

        // AsSplitQuery：这里 Include 了 3 个集合导航，单条 SQL 会产生笛卡尔积，
        // 拆成多条查询走 join 再聚合，数据量上来后不会拖垮查询
        var account = await _db.Accounts
            .AsNoTracking()
            .AsSplitQuery()
            .Include(a => a.Characters)
            .Include(a => a.EngravingsUnlocked)
            .Include(a => a.EngravingsSelected)
            .FirstOrDefaultAsync(a => a.Id == accountId);

        if (account == null)
            return Unauthorized(ApiResult<ProfileDto>.Fail(ApiCode.Unauthorized, "账号不存在"));
        if (account.Status == 0)
            return Ok(ApiResult<ProfileDto>.Fail(ApiCode.AccountBanned, "账号已被封禁"));

        var characters = account.Characters
            .Where(c => c.Owned)
            .Select(c => new CharacterOwnedDto(c.CharacterUid, true))
            .ToList();

        var engravings = new EngravingProgressDto(
            account.EngravingsUnlocked.Select(e => e.EngravingId).ToList(),
            account.EngravingsSelected.OrderBy(e => e.SlotIndex).Select(e => e.EngravingId).ToList());

        var dto = new ProfileDto(account.Id, account.Username, account.Coins, characters, engravings,
            account.PendingSession ?? string.Empty);
        return Ok(ApiResult<ProfileDto>.Success(dto));
    }
}
