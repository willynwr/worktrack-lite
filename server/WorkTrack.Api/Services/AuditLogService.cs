namespace WorkTrack.Api.Services;

using WorkTrack.Api.Data;
using WorkTrack.Api.Data.Entities;

/// <summary>Mencatat aksi admin sensitif ke tabel AuditLogs.</summary>
public class AuditLogService
{
    private readonly AppDbContext _db;

    public AuditLogService(AppDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(string adminUsername, string action, string? target, string? ipAddress, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            AdminUsername = adminUsername,
            Action        = action,
            Target        = target,
            IpAddress     = ipAddress,
            Timestamp     = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }
}
