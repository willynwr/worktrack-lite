namespace WorkTrack.Api.Data.Entities;

/// <summary>EF Core entity untuk tabel `screenshots` di MySQL.</summary>
public class Screenshot
{
    public long Id { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    /// <summary>UUID yang sama dengan ActivityReport — menghubungkan screenshot ke report.</summary>
    public string ClientUuid { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }

    /// <summary>0 = monitor utama (atau virtual screen gabungan semua monitor).</summary>
    public int MonitorIndex { get; set; }

    /// <summary>Path relatif di IScreenshotStore, mis. "PC-001/2026-08-21/20260821113000_0.jpg".</summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>"image/jpeg" | "image/webp"</summary>
    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation property
    public Device Device { get; set; } = null!;
}
