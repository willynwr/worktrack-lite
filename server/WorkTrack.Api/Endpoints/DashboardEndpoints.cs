namespace WorkTrack.Api.Endpoints;

using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using WorkTrack.Api.Data;
using WorkTrack.Api.Services;
using WorkTrack.Api.Storage;

/// <summary>
/// Endpoint khusus untuk Web Dashboard (Next.js).
/// Authentication sementara menggunakan device token yang sama;
/// Phase 6 akan menambahkan admin session auth terpisah.
/// </summary>
public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/dashboard")
            .WithTags("Dashboard")
            .AddEndpointFilter(AdminJwtFilter);  // ← setiap endpoint dashboard wajib admin JWT

        g.MapGet("/devices",               GetAllDevices);
        g.MapGet("/devices/{id}",          GetDeviceDetail);
        g.MapGet("/devices/{id}/timeline", GetTimeline);
        g.MapGet("/devices/{id}/stats",    GetStats);
        g.MapPatch("/devices/{id}",        PatchDevice);
        g.MapGet("/devices/{id}/screenshots/download", DownloadScreenshots);
    }

    // ── Admin JWT endpoint filter ────────────────────────────────────────────
    private static async ValueTask<object?> AdminJwtFilter(
        EndpointFilterInvocationContext ctx,
        EndpointFilterDelegate next)
    {
        // Token bisa dari header atau cookie (untuk server-component Next.js)
        var token = ctx.HttpContext.Request.Headers.Authorization
            .FirstOrDefault()?.Replace("Bearer ", "");

        if (string.IsNullOrEmpty(token))
            token = ctx.HttpContext.Request.Cookies["admin_token"];

        if (string.IsNullOrEmpty(token))
            return Results.Json(new { error = "Admin authentication required." }, statusCode: 401);

        var jwtSvc   = ctx.HttpContext.RequestServices.GetRequiredService<WorkTrack.Api.Auth.JwtService>();
        var username = jwtSvc.ValidateToken(token);

        if (username is null)
            return Results.Json(new { error = "Token tidak valid atau sudah kadaluarsa." }, statusCode: 401);

        ctx.HttpContext.Items["AdminUser"] = username;
        return await next(ctx);
    }


    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/v1/dashboard/devices
    // Daftar semua device + status online/offline + activity terakhir
    // ─────────────────────────────────────────────────────────────────────────
    private static async Task<IResult> GetAllDevices(AppDbContext db)
    {
        var now      = DateTimeOffset.UtcNow;
        var offline  = TimeSpan.FromMinutes(3);  // > 3× interval heartbeat 60s

        var devices = await db.Devices
            .OrderBy(d => d.DeviceId)
            .Select(d => new
            {
                d.DeviceId,
                d.Hostname,
                d.LocalIp,
                d.WindowsVersion,
                d.AgentVersion,
                d.RegisteredAt,
                d.LastSeenAt,
                d.IsActive,
                IsOnline = d.LastSeenAt != null &&
                           (now - d.LastSeenAt.Value) < offline
            })
            .ToListAsync();

        // Enrich dengan last active_app + idle per device (subquery-per-device, cukup untuk 10 PC)
        var enriched = new List<object>();
        foreach (var d in devices)
        {
            var last = await db.ActivityReports
                .Where(r => r.DeviceId == d.DeviceId)
                .OrderByDescending(r => r.Timestamp)
                .Select(r => new { r.ActiveApp, r.IdleSeconds, r.UptimeSeconds, r.Timestamp })
                .FirstOrDefaultAsync();

            enriched.Add(new
            {
                d.DeviceId,
                d.Hostname,
                d.LocalIp,
                d.WindowsVersion,
                d.AgentVersion,
                d.RegisteredAt,
                d.LastSeenAt,
                d.IsActive,
                d.IsOnline,
                LastActiveApp    = last?.ActiveApp,
                LastIdleSeconds  = last?.IdleSeconds,
                LastUptimeSeconds = last?.UptimeSeconds,
                LastReportAt     = last?.Timestamp
            });
        }

        return Results.Ok(enriched);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/v1/dashboard/devices/{id}
    // ─────────────────────────────────────────────────────────────────────────
    private static async Task<IResult> GetDeviceDetail(string id, AppDbContext db)
    {
        var device = await db.Devices.FindAsync(id);
        if (device is null) return Results.NotFound();

        var now     = DateTimeOffset.UtcNow;
        var offline = TimeSpan.FromMinutes(3);

        var last = await db.ActivityReports
            .Where(r => r.DeviceId == id)
            .OrderByDescending(r => r.Timestamp)
            .Select(r => new { r.ActiveApp, r.IdleSeconds, r.UptimeSeconds, r.Timestamp })
            .FirstOrDefaultAsync();

        var lastScreenshot = await db.Screenshots
            .Where(s => s.DeviceId == id)
            .OrderByDescending(s => s.Timestamp)
            .Select(s => new { s.Id, s.Timestamp, FileUrl = $"/api/v1/screenshots/file/{s.Id}" })
            .FirstOrDefaultAsync();

        return Results.Ok(new
        {
            device.DeviceId,
            device.Hostname,
            device.LocalIp,
            device.WindowsVersion,
            device.AgentVersion,
            device.RegisteredAt,
            device.LastSeenAt,
            device.IsActive,
            IsOnline          = device.LastSeenAt != null && (now - device.LastSeenAt.Value) < offline,
            LastActiveApp     = last?.ActiveApp,
            LastIdleSeconds   = last?.IdleSeconds,
            LastUptimeSeconds = last?.UptimeSeconds,
            LastReportAt      = last?.Timestamp,
            LastScreenshot    = lastScreenshot
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/v1/dashboard/devices/{id}/timeline?date=YYYY-MM-DD
    // Activity reports + screenshot URL terurut per menit
    // ─────────────────────────────────────────────────────────────────────────
    private static async Task<IResult> GetTimeline(
        string id,
        string? date,
        AppDbContext db)
    {
        var targetDate = string.IsNullOrWhiteSpace(date)
            ? DateOnly.FromDateTime(DateTime.UtcNow)
            : DateOnly.TryParse(date, out var parsed) ? parsed : DateOnly.FromDateTime(DateTime.UtcNow);

        var start = new DateTimeOffset(targetDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end   = start.AddDays(1);

        // Activity reports
        var reports = await db.ActivityReports
            .Where(r => r.DeviceId == id && r.Timestamp >= start && r.Timestamp < end)
            .OrderBy(r => r.Timestamp)
            .Select(r => new
            {
                r.Id,
                r.ClientUuid,
                r.Timestamp,
                r.ActiveApp,
                r.IdleSeconds,
                r.UptimeSeconds
            })
            .ToListAsync();

        // Screenshot metadata (join ke report via client_uuid)
        var screenshots = await db.Screenshots
            .Where(s => s.DeviceId == id && s.Timestamp >= start && s.Timestamp < end)
            .OrderBy(s => s.Timestamp)
            .Select(s => new
            {
                s.Id,
                s.ClientUuid,
                s.Timestamp,
                s.MonitorIndex,
                s.SizeBytes,
                FileUrl = $"/api/v1/screenshots/file/{s.Id}"
            })
            .ToListAsync();

        // Merge: gabungkan tiap report dengan screenshot-nya (via client_uuid)
        var ssMap = screenshots.GroupBy(s => s.ClientUuid)
                               .ToDictionary(g => g.Key, g => g.First());

        var timeline = reports.Select(r => new
        {
            r.Timestamp,
            r.ActiveApp,
            r.IdleSeconds,
            r.UptimeSeconds,
            Screenshot = ssMap.TryGetValue(r.ClientUuid, out var ss) ? (object)new
            {
                ss.Id,
                ss.FileUrl,
                ss.SizeBytes
            } : null
        }).ToList();

        return Results.Ok(new { Date = targetDate.ToString("yyyy-MM-dd"), Timeline = timeline });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/v1/dashboard/devices/{id}/stats?date=YYYY-MM-DD
    // Statistik harian dihitung on-read
    // ─────────────────────────────────────────────────────────────────────────
    private static async Task<IResult> GetStats(
        string id,
        string? date,
        AppDbContext db)
    {
        var targetDate = string.IsNullOrWhiteSpace(date)
            ? DateOnly.FromDateTime(DateTime.UtcNow)
            : DateOnly.TryParse(date, out var parsed) ? parsed : DateOnly.FromDateTime(DateTime.UtcNow);

        var start = new DateTimeOffset(targetDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end   = start.AddDays(1);

        var reports = await db.ActivityReports
            .Where(r => r.DeviceId == id && r.Timestamp >= start && r.Timestamp < end)
            .ToListAsync();

        if (reports.Count == 0)
            return Results.Ok(new
            {
                Date           = targetDate.ToString("yyyy-MM-dd"),
                TotalRecords   = 0,
                TotalActiveSeconds   = 0,
                TotalIdleSeconds     = 0,
                MaxUptimeSeconds     = 0L,
                ScreenshotCount      = 0,
                TopApps              = Array.Empty<object>()
            });

        // Active = record dengan idle_seconds < 60 (pengguna menggerak mouse/keyboard selama 60s terakhir)
        var totalActive = reports.Count(r => r.IdleSeconds < 60) * 60;
        var totalIdle   = reports.Count(r => r.IdleSeconds >= 60) * 60;
        var maxUptime   = reports.Max(r => r.UptimeSeconds);

        // App usage: hitung berapa record per app (≈ menit aktif)
        var topApps = reports
            .Where(r => r.ActiveApp != null && r.IdleSeconds < 60)
            .GroupBy(r => r.ActiveApp!)
            .Select(g => new { App = g.Key, Minutes = g.Count() })
            .OrderByDescending(x => x.Minutes)
            .Take(10)
            .ToList();

        var screenshotCount = await db.Screenshots
            .CountAsync(s => s.DeviceId == id && s.Timestamp >= start && s.Timestamp < end);

        return Results.Ok(new
        {
            Date                  = targetDate.ToString("yyyy-MM-dd"),
            TotalRecords          = reports.Count,
            TotalActiveSeconds    = totalActive,
            TotalIdleSeconds      = totalIdle,
            MaxUptimeSeconds      = maxUptime,
            ScreenshotCount       = screenshotCount,
            TopApps               = topApps
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH /api/v1/dashboard/devices/{id}  — { is_active: bool }
    // Admin enable/disable device
    // ─────────────────────────────────────────────────────────────────────────
    private static async Task<IResult> PatchDevice(
        string id,
        PatchDeviceRequest body,
        AppDbContext db,
        AuditLogService auditLog,
        HttpContext httpContext,
        ILogger<AppDbContext> logger)
    {
        var device = await db.Devices.FindAsync(id);
        if (device is null) return Results.NotFound();

        device.IsActive = body.IsActive;
        await db.SaveChangesAsync();

        logger.LogInformation("Device {Id} set IsActive={Active}", id, body.IsActive);

        var adminUsername = httpContext.Items["AdminUser"]?.ToString() ?? "unknown";
        var action         = body.IsActive ? "enabled_device" : "disabled_device";
        await auditLog.LogAsync(adminUsername, action, id, httpContext.Connection.RemoteIpAddress?.ToString());

        return Results.Ok(new { device.DeviceId, device.IsActive });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/v1/dashboard/devices/{id}/screenshots/download?from=YYYY-MM-DD&to=YYYY-MM-DD
    // Download kolektif sebagai satu file ZIP.
    // ─────────────────────────────────────────────────────────────────────────
    private static async Task<IResult> DownloadScreenshots(
        string id,
        string? from,
        string? to,
        AppDbContext db,
        IScreenshotStore store,
        AuditLogService auditLog,
        HttpContext httpContext,
        ILogger<AppDbContext> logger)
    {
        var today    = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDay = DateOnly.TryParse(from, out var f) ? f : today.AddDays(-6);
        var endDay   = DateOnly.TryParse(to, out var t) ? t : today;

        var start = new DateTimeOffset(startDay.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end   = new DateTimeOffset(endDay.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(1);

        var screenshots = await db.Screenshots
            .Where(s => s.DeviceId == id && s.Timestamp >= start && s.Timestamp < end)
            .OrderBy(s => s.Timestamp)
            .ToListAsync();

        if (screenshots.Count == 0)
            return Results.NotFound(new { error = "Tidak ada screenshot pada rentang tanggal ini." });

        byte[] zipBytes;
        using (var memoryStream = new MemoryStream())
        {
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var s in screenshots)
                {
                    var fileStream = await store.GetAsync(s.StoragePath);
                    if (fileStream is null) continue;

                    var ext   = s.ContentType.Contains("webp") ? "webp" : "jpg";
                    var entry = archive.CreateEntry($"{s.Timestamp:yyyyMMdd_HHmmss}_{s.Id}.{ext}", CompressionLevel.Fastest);

                    using var entryStream = entry.Open();
                    using (fileStream)
                        await fileStream.CopyToAsync(entryStream);
                }
            }
            zipBytes = memoryStream.ToArray();
        }

        var adminUsername = httpContext.Items["AdminUser"]?.ToString() ?? "unknown";
        await auditLog.LogAsync(adminUsername, "downloaded_screenshots", $"{id}:{startDay}..{endDay}",
            httpContext.Connection.RemoteIpAddress?.ToString());

        logger.LogInformation("Screenshots downloaded: device={DeviceId} range={Start}..{End} count={Count}",
            id, startDay, endDay, screenshots.Count);

        return Results.File(zipBytes, "application/zip", $"{id}_{startDay:yyyyMMdd}_{endDay:yyyyMMdd}.zip");
    }
}

public record PatchDeviceRequest(bool IsActive);
