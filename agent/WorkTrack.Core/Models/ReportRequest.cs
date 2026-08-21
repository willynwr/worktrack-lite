namespace WorkTrack.Core.Models;

public class ReportRequest
{
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>UUID unik per record — kunci idempotency (UNIQUE device_id + client_uuid di server).</summary>
    public string ClientUuid { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Nama executable foreground saja (mis. "chrome.exe"). Null bila tidak ada foreground app.</summary>
    public string? ActiveApp { get; set; }

    /// <summary>Detik idle sejak input terakhir (GetLastInputInfo).</summary>
    public int IdleSeconds { get; set; }

    /// <summary>Uptime OS dalam detik (GetTickCount64).</summary>
    public long UptimeSeconds { get; set; }
}
