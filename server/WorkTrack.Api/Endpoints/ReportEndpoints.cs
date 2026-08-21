namespace WorkTrack.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using WorkTrack.Api.Data;
using WorkTrack.Api.Data.Entities;
using WorkTrack.Core.Models;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/reports").WithTags("Reports");

        group.MapPost("/", SubmitReport)
             .WithName("SubmitReport")
             .WithSummary("Terima activity report dari agent. Idempotent by (device_id, client_uuid).");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/v1/reports
    // ─────────────────────────────────────────────────────────────────────────
    private static async Task<IResult> SubmitReport(
        ReportRequest request,
        HttpContext httpContext,
        AppDbContext db,
        ILogger<AppDbContext> logger)
    {
        // Validasi auth
        var deviceId = httpContext.Items["DeviceId"]?.ToString();
        if (deviceId is null)
            return Results.Json(new { error = "Unauthorized: valid device token required." }, statusCode: 401);

        // DeviceId di body harus cocok dengan token yang dipakai
        if (request.DeviceId != deviceId)
            return Results.Json(new { error = "DeviceId mismatch." }, statusCode: 403);

        if (string.IsNullOrWhiteSpace(request.ClientUuid))
            return Results.BadRequest(new { error = "client_uuid is required." });

        // ── Idempotency check ──
        var existing = await db.ActivityReports
            .FirstOrDefaultAsync(r => r.DeviceId == deviceId && r.ClientUuid == request.ClientUuid);

        if (existing is not null)
        {
            logger.LogDebug("Duplicate report: device={DeviceId} uuid={Uuid}", deviceId, request.ClientUuid);
            return Results.Ok(new ReportResponse
            {
                ReportId  = existing.Id,
                Accepted  = true,
                Duplicate = true
            });
        }

        // ── Simpan record baru ──
        var report = new ActivityReport
        {
            DeviceId      = deviceId,
            ClientUuid    = request.ClientUuid,
            Timestamp     = request.Timestamp,
            ActiveApp     = request.ActiveApp,
            IdleSeconds   = request.IdleSeconds,
            UptimeSeconds = request.UptimeSeconds,
            CreatedAt     = DateTimeOffset.UtcNow
        };

        db.ActivityReports.Add(report);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Report saved: device={DeviceId} app={App} idle={Idle}s id={Id}",
            deviceId, request.ActiveApp ?? "none", request.IdleSeconds, report.Id);

        return Results.Created($"/api/v1/reports/{report.Id}", new ReportResponse
        {
            ReportId  = report.Id,
            Accepted  = true,
            Duplicate = false
        });
    }
}
