namespace WorkTrack.Core.Models;

public class HeartbeatRequest
{
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>Local IP terkini — boleh null jika tidak berubah.</summary>
    public string? LocalIp { get; set; }

    /// <summary>Uptime OS dalam detik — dari GetTickCount64.</summary>
    public long UptimeSeconds { get; set; }
}
