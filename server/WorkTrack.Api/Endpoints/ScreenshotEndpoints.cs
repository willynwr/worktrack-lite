namespace WorkTrack.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using WorkTrack.Api.Auth;
using WorkTrack.Api.Data;
using WorkTrack.Api.Data.Entities;
using WorkTrack.Api.Services;
using WorkTrack.Api.Storage;

public static class ScreenshotEndpoints
{
    public static void MapScreenshotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Screenshots");

        // Upload screenshot dari agent
        group.MapPost("/screenshots", UploadScreenshot)
             .WithName("UploadScreenshot")
             .DisableAntiforgery();

        // Metadata list per device per hari
        group.MapGet("/devices/{deviceId}/screenshots", GetScreenshots)
             .WithName("GetScreenshots");

        // Serve file screenshot langsung
        group.MapGet("/screenshots/file/{id:long}", ServeScreenshot)
             .WithName("ServeScreenshot");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/v1/screenshots
    // ─────────────────────────────────────────────────────────────────────────
    private static async Task<IResult> UploadScreenshot(
        HttpContext httpContext,
        IFormFile file,
        AppDbContext db,
        IScreenshotStore store,
        ILogger<AppDbContext> logger)
    {
        // Validasi auth
        var deviceId = httpContext.Items["DeviceId"]?.ToString();
        if (deviceId is null)
            return Results.Json(new { error = "Unauthorized." }, statusCode: 401);

        // Parse form fields
        var form        = httpContext.Request.Form;
        var clientUuid  = form["client_uuid"].FirstOrDefault();
        var timestampRaw = form["timestamp"].FirstOrDefault();
        var monitorStr  = form["monitor_index"].FirstOrDefault() ?? "0";

        if (string.IsNullOrWhiteSpace(clientUuid))
            return Results.BadRequest(new { error = "client_uuid is required." });

        if (!DateTimeOffset.TryParse(timestampRaw, out var timestamp))
            timestamp = DateTimeOffset.UtcNow;

        if (!int.TryParse(monitorStr, out var monitorIndex))
            monitorIndex = 0;

        // Idempotency check
        var existing = await db.Screenshots.FirstOrDefaultAsync(
            s => s.DeviceId == deviceId && s.ClientUuid == clientUuid && s.MonitorIndex == monitorIndex);

        if (existing is not null)
        {
            return Results.Ok(new
            {
                screenshot_id = existing.Id,
                accepted      = true,
                duplicate     = true
            });
        }

        // Validasi content type
        var contentType = file.ContentType;
        if (contentType != "image/jpeg" && contentType != "image/webp")
            contentType = "image/jpeg"; // default fallback

        // Simpan ke store
        string storagePath;
        await using (var stream = file.OpenReadStream())
        {
            storagePath = await store.SaveAsync(deviceId, timestamp, monitorIndex, stream, contentType);
        }

        // Simpan metadata ke DB
        var screenshot = new Screenshot
        {
            DeviceId    = deviceId,
            ClientUuid  = clientUuid,
            Timestamp   = timestamp,
            MonitorIndex = monitorIndex,
            StoragePath = storagePath,
            ContentType = contentType,
            SizeBytes   = file.Length,
            CreatedAt   = DateTimeOffset.UtcNow
        };

        db.Screenshots.Add(screenshot);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Screenshot saved: device={DeviceId} monitor={Monitor} size={Kb}KB id={Id}",
            deviceId, monitorIndex, file.Length / 1024, screenshot.Id);

        // Riwayat screenshot disimpan (bukan cuma 1 terbaru) — dibersihkan berkala oleh
        // RetentionService berdasarkan umur (Admin:RetentionDays), bukan dihapus tiap upload.

        return Results.Created($"/api/v1/screenshots/file/{screenshot.Id}", new
        {
            screenshot_id = screenshot.Id,
            accepted      = true,
            duplicate     = false
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/v1/devices/{deviceId}/screenshots?date=YYYY-MM-DD
    // ─────────────────────────────────────────────────────────────────────────
    private static async Task<IResult> GetScreenshots(
        string deviceId,
        string? date,
        AppDbContext db,
        HttpContext httpContext)
    {
        // Hanya admin yang bisa lihat (Phase 6: auth dashboard)
        // Untuk sekarang: device token boleh lihat device-nya sendiri
        var callerDeviceId = httpContext.Items["DeviceId"]?.ToString();
        if (callerDeviceId is null)
            return Results.Json(new { error = "Unauthorized." }, statusCode: 401);

        var query = db.Screenshots.Where(s => s.DeviceId == deviceId);

        if (!string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var parsedDate))
        {
            var start = new DateTimeOffset(parsedDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var end   = start.AddDays(1);
            query = query.Where(s => s.Timestamp >= start && s.Timestamp < end);
        }

        var results = await query
            .OrderBy(s => s.Timestamp)
            .Select(s => new
            {
                s.Id,
                s.DeviceId,
                s.ClientUuid,
                s.Timestamp,
                s.MonitorIndex,
                s.ContentType,
                s.SizeBytes,
                FileUrl = $"/api/v1/screenshots/file/{s.Id}"
            })
            .ToListAsync();

        return Results.Ok(results);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/v1/screenshots/file/{id}
    // ─────────────────────────────────────────────────────────────────────────
    private static async Task<IResult> ServeScreenshot(
        long id,
        AppDbContext db,
        IScreenshotStore store,
        JwtService jwtService,
        AuditLogService auditLog,
        HttpContext httpContext)
    {
        // Diizinkan untuk: (a) device pemilik screenshot via device token, atau
        // (b) admin dashboard via JWT (header Authorization atau cookie admin_token —
        //     <img src> browser tidak bisa menyisipkan header custom, jadi cookie penting di sini).
        var callerDeviceId = httpContext.Items["DeviceId"]?.ToString();
        var adminUsername  = ResolveAdminUsername(httpContext, jwtService);

        if (callerDeviceId is null && adminUsername is null)
            return Results.Json(new { error = "Unauthorized." }, statusCode: 401);

        var screenshot = await db.Screenshots.FindAsync(id);
        if (screenshot is null)
            return Results.NotFound();

        var stream = await store.GetAsync(screenshot.StoragePath);
        if (stream is null)
            return Results.NotFound();

        if (adminUsername is not null)
        {
            await auditLog.LogAsync(adminUsername, "viewed_screenshot", id.ToString(),
                httpContext.Connection.RemoteIpAddress?.ToString());
        }

        return Results.Stream(stream, screenshot.ContentType);
    }

    private static string? ResolveAdminUsername(HttpContext httpContext, JwtService jwtService)
    {
        var token = httpContext.Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");
        if (string.IsNullOrEmpty(token))
            token = httpContext.Request.Cookies["admin_token"];

        return string.IsNullOrEmpty(token) ? null : jwtService.ValidateToken(token);
    }
}
