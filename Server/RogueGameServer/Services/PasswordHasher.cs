using System.Security.Cryptography;

namespace RogueGameServer.Services;

/// <summary>
/// 密码哈希：PBKDF2-HMACSHA256，每账号独立随机盐。
/// 库里绝不存明文密码，也不存可逆结果，拖库也拿不到原密码。
/// </summary>
public static class PasswordHasher
{
    private const int Iterations = 10000;
    private const int SaltSize = 16;
    private const int HashSize = 64;

    public static (byte[] Hash, byte[] Salt) Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        return (pbkdf2.GetBytes(HashSize), salt);
    }

    public static bool Verify(string password, byte[] expectedHash, byte[] salt)
    {
        if (expectedHash.Length == 0) return false;
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        byte[] actual = pbkdf2.GetBytes(expectedHash.Length);
        // 固定时间比较，避免通过响应耗时逐字节试探
        return CryptographicOperations.FixedTimeEquals(actual, expectedHash);
    }
}
