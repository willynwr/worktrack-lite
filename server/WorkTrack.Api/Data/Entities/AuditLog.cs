namespace WorkTrack.Api.Data.Entities;

/// <summary>Audit log untuk aksi admin sensitif (lihat screenshot, disable device, dll).</summary>
public class AuditLog
{
    public long Id { get; set; }
    public string AdminUsername { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // viewed_screenshot, disabled_device, etc.
    public string? Target { get; set; }                // device_id, screenshot_id, dll
    public DateTimeOffset Timestamp { get; set; }
    public string? IpAddress { get; set; }
}
