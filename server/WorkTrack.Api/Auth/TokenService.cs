namespace WorkTrack.Api.Auth;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Utility untuk generate dan hash device token.
/// Token format: "wt_live_{base64url_32bytes}"
/// Hash  format: SHA-256 hex lowercase (64 chars) — yang disimpan di DB.
/// </summary>
public static class TokenService
{
    /// <summary>Generate token acak untuk device baru. Kembalikan SEKALI ke agent, lalu buang.</summary>
    public static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var base64 = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        return $"wt_live_{base64}";
    }

    /// <summary>Hash token untuk disimpan di DB (bukan plaintext).</summary>
    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Verifikasi token menggunakan constant-time comparison (anti timing attack).</summary>
    public static bool VerifyToken(string token, string storedHash)
    {
        var computedHash = HashToken(token);
        // Pad/trim agar panjang sama sebelum compare (SHA-256 = 64 chars, selalu sama)
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHash),
            Encoding.UTF8.GetBytes(storedHash)
        );
    }
}
