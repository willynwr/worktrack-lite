namespace WorkTrack.Api.Auth;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Generate dan validasi JWT untuk admin dashboard.
/// Secret dikonfigurasi via Admin:JwtSecret di appsettings.json.
/// Wajib diganti dari nilai default di production!
/// </summary>
public class JwtService
{
    private readonly string _secret;
    private readonly int _expiryHours;

    private const string Issuer   = "worktrack-api";
    private const string Audience = "worktrack-dashboard";

    public JwtService(IConfiguration config)
    {
        _secret      = config["Admin:JwtSecret"]
            ?? throw new InvalidOperationException("Admin:JwtSecret tidak dikonfigurasi di appsettings.json.");
        _expiryHours = int.Parse(config["Admin:JwtExpiryHours"] ?? "8");
    }

    /// <summary>Generate JWT token untuk admin. Berlaku selama JwtExpiryHours.</summary>
    public string GenerateToken(string username)
    {
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,  username),
            new Claim(JwtRegisteredClaimNames.Jti,  Guid.NewGuid().ToString()),
            new Claim("role", "admin"),
        };

        var token = new JwtSecurityToken(
            issuer:             Issuer,
            audience:           Audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(_expiryHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Validasi token dan kembalikan username bila valid.
    /// Kembalikan null bila tidak valid / kadaluarsa.
    /// </summary>
    public string? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidIssuer              = Issuer,
                ValidateAudience         = true,
                ValidAudience            = Audience,
                ValidateLifetime         = true,
                IssuerSigningKey         = key,
                ClockSkew                = TimeSpan.Zero,
            }, out _);

            return principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        }
        catch
        {
            return null;
        }
    }
}
