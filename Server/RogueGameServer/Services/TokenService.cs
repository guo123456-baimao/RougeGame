using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace RogueGameServer.Services;

/// <summary>
/// JWT 签发。HS256 对称签名：密钥只在服务端，客户端无法伪造 accountId。
/// </summary>
public class TokenService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly SigningCredentials _credentials;
    private readonly int _expireDays;

    public TokenService(IConfiguration configuration)
    {
        string key = configuration["Jwt:Key"]
                     ?? throw new InvalidOperationException("缺少 Jwt:Key 配置");
        _issuer = configuration["Jwt:Issuer"] ?? "RogueGameServer";
        _audience = configuration["Jwt:Audience"] ?? "RogueGameClient";
        _expireDays = int.TryParse(configuration["Jwt:ExpireDays"], out int days) ? days : 7;
        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);
    }

    public (string Token, long ExpiresIn) CreateToken(long accountId, string username)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, accountId.ToString()),
            new Claim(ClaimTypes.Name, username)
        };

        var expires = DateTime.UtcNow.AddDays(_expireDays);
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expires,
            signingCredentials: _credentials);

        long expiresIn = (long)(expires - DateTime.UtcNow).TotalSeconds;
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresIn);
    }
}
