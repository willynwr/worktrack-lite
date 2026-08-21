namespace WorkTrack.SessionAgent.Uptime;

/// <summary>
/// Membaca uptime OS (bukan uptime proses) menggunakan Environment.TickCount64.
/// Setara dengan GetTickCount64() Win32 API.
/// </summary>
public static class UptimeMonitor
{
    /// <summary>Kembalikan uptime OS dalam detik sejak boot terakhir.</summary>
    public static long GetUptimeSeconds() =>
        (long)TimeSpan.FromMilliseconds(Environment.TickCount64).TotalSeconds;
}
