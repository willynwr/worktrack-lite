namespace WorkTrack.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using WorkTrack.Api.Auth;
using WorkTrack.Api.Data;
using WorkTrack.Api.Data.Entities;
using WorkTrack.Core.Models;

public static class DeviceEndpoints
{
    public static void MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/devices").WithTags("Devices");

        // Public — tidak butuh token
        group.MapPost("/register", RegisterDevice)
             .WithName("RegisterDevice")
             .RequireRateLimiting("register")
             .WithSummary("Daftarkan device baru atau kembalikan device_id yang sudah ada.");

        // Protected — butuh valid device token
        group.MapPost("/heartbeat", Heartbeat)
             .WithName("Heartbeat")
             .WithSummary("Perbarui last_seen_at dan local_ip device.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/v1/devices/register
    // ─────────────────────────────────────────────────────────────────────────
    private static async Task<IResult> RegisterDevice(
        RegisterRequest request,
        AppDbContext db,
        ILogger<AppDbContext> logger)
    {
        if (string.IsNullOrWhiteSpace(request.MachineKey))
            return Results.BadRequest(new { error = "machine_key is required." });

        // ── Cek apakah machine_key sudah terdaftar ──
        var existing = await db.Devices
            .FirstOrDefaultAsync(d => d.MachineKey == request.MachineKey);

        if (existing is not null)
        {
            logger.LogInformation(
                "Re-register attempt for existing device {DeviceId} ({Hostname})",
                existing.DeviceId, existing.Hostname);

            // Kembalikan device_id tapi BUKAN token (token sudah disimpan agent via DPAPI)
            return Results.Ok(new
            {
                device_id    = existing.DeviceId,
                device_token = (string?)null,
                status       = "already_registered",
                server_time  = DateTimeOffset.UtcNow
            });
        }

        // ── Buat device baru ──
        var deviceCount = await db.Devices.CountAsync();
        var deviceId    = $"PC-{(deviceCount + 1):D3}";
        var token       = TokenService.GenerateToken();
        var tokenHash   = TokenService.HashToken(token);

        var device = new Device
        {
            DeviceId       = deviceId,
            Hostname       = request.Hostname,
            WindowsVersion = request.WindowsVersion,
            AgentVersion   = request.AgentVersion,
            LocalIp        = request.LocalIp,
            MachineKey     = request.MachineKey,
            TokenHash      = tokenHash,
            RegisteredAt   = DateTimeOffset.UtcNow,
            IsActive       = true
        };

        db.Devices.Add(device);
        await db.SaveChangesAsync();

        logger.LogInformation("Device registered: {DeviceId} | {Hostname} | {IP}", deviceId, request.Hostname, request.LocalIp);

        return Results.Created(
            $"/api/v1/devices/{deviceId}",
            new RegisterResponse
            {
                DeviceId    = deviceId,
                DeviceToken = token,    // plaintext, dikirim SEKALI
                Status      = "registered",
                ServerTime  = DateTimeOffset.UtcNow
            }
        );
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/v1/devices/heartbeat
    // ─────────────────────────────────────────────────────────────────────────
    private static async Task<IResult> Heartbeat(
        HeartbeatRequest request,
        HttpContext httpContext,
        AppDbContext db,
        ILogger<AppDbContext> logger)
    {
        // DeviceAuthMiddleware sudah validasi token dan set DeviceId
        var deviceId = httpContext.Items["DeviceId"]?.ToString();
        if (deviceId is null)
            return Results.Json(new { error = "Unauthorized: valid device token required." }, statusCode: 401);

        var device = await db.Devices.FindAsync(deviceId);
        if (device is null || !device.IsActive)
            return Results.Json(new { error = "Device not found or deactivated." }, statusCode: 403);

        device.LastSeenAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrEmpty(request.LocalIp))
            device.LocalIp = request.LocalIp;

        await db.SaveChangesAsync();

        logger.LogDebug("Heartbeat OK: {DeviceId} (uptime {Uptime}s)", deviceId, request.UptimeSeconds);

        return Results.Ok(new HeartbeatResponse
        {
            Status     = "ok",
            ServerTime = DateTimeOffset.UtcNow
        });
    }
}
