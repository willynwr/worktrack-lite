namespace WorkTrack.Api.Endpoints;

using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using WorkTrack.Api.Auth;
using WorkTrack.Api.Data;
using WorkTrack.Api.Data.Entities;

/// <summary>
/// Endpoint admin: login + seed admin pertama.
///
/// POST /api/v1/admin/login  — kembalikan JWT (tidak butuh auth)
/// POST /api/v1/admin/seed   — buat admin pertama (hanya bila belum ada admin)
/// </summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/admin").WithTags("Admin Auth");
        g.MapPost("/login", Login).WithName("AdminLogin").RequireRateLimiting("admin_login");
        g.MapPost("/seed",  Seed).WithName("AdminSeed");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/v1/admin/login
    // ─────────────────────────────────────────────────────────────────────────
    private static async Task<IResult> Login(
        LoginRequest req,
        AppDbContext db,
        JwtService jwt,
        ILogger<AdminUser> logger,
        HttpContext ctx)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return Results.BadRequest(new { error = "Username dan password wajib diisi." });

        var admin = await db.AdminUsers
            .FirstOrDefaultAsync(a => a.Username == req.Username);

        // Constant-time check: selalu panggil BCrypt meski user tidak ditemukan
        var hash  = admin?.PasswordHash ?? "$2a$11$dummy_hash_to_prevent_timing_attack_xxxxxxxxx";
        var valid = BCrypt.Verify(req.Password, hash);

        if (admin is null || !valid)
        {
            logger.LogWarning("Failed login attempt: username={User} ip={IP}",
                req.Username, ctx.Connection.RemoteIpAddress);
            return Results.Json(new { error = "Username atau password salah." }, statusCode: 401);
        }

        admin.LastLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var token = jwt.GenerateToken(admin.Username);
        logger.LogInformation("Admin login: {User} from {IP}", admin.Username, ctx.Connection.RemoteIpAddress);

        return Results.Ok(new { token, expires_in_hours = 8 });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/v1/admin/seed   — hanya berjalan sekali (bila belum ada admin)
    // Body: { "username": "admin", "password": "..." }
    // ─────────────────────────────────────────────────────────────────────────
    private static async Task<IResult> Seed(
        LoginRequest req,
        AppDbContext db,
        ILogger<AdminUser> logger)
    {
        if (await db.AdminUsers.AnyAsync())
            return Results.Json(new { error = "Admin sudah ada. Endpoint seed dinonaktifkan." }, statusCode: 409);

        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return Results.BadRequest(new { error = "Username dan password wajib diisi." });

        if (req.Password.Length < 8)
            return Results.BadRequest(new { error = "Password minimal 8 karakter." });

        var admin = new AdminUser
        {
            Username     = req.Username.Trim(),
            PasswordHash = BCrypt.HashPassword(req.Password, workFactor: 12),
            CreatedAt    = DateTimeOffset.UtcNow
        };

        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync();

        logger.LogInformation("Admin seeded: username={User}", admin.Username);
        return Results.Created("/api/v1/admin/login", new { message = "Admin berhasil dibuat. Silakan login." });
    }
}

public record LoginRequest(string Username, string Password);
