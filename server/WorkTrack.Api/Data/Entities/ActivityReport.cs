namespace WorkTrack.Api.Data.Entities;

/// <summary>EF Core entity untuk tabel `activity_reports` di MySQL.</summary>
public class ActivityReport
{
    public long Id { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    /// <summary>UUID unik per record — kunci idempotency.</summary>
    public string ClientUuid { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Nama executable foreground (mis. "chrome.exe"). Null bila idle/desktop.</summary>
    public string? ActiveApp { get; set; }

    public int IdleSeconds { get; set; }
    public long UptimeSeconds { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation property
    public Device Device { get; set; } = null!;
}
