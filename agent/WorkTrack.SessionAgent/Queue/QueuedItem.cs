namespace WorkTrack.SessionAgent.Queue;

/// <summary>Record yang antri untuk dikirim ulang (activity report).</summary>
public sealed class QueuedReport
{
    public long Id { get; init; }

    /// <summary>JSON dari ReportRequest — disimpan lengkap untuk retry tanpa data loss.</summary>
    public string PayloadJson { get; init; } = string.Empty;

    public int RetryCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Record yang antri untuk dikirim ulang (screenshot file).</summary>
public sealed class QueuedScreenshot
{
    public long Id { get; init; }
    public string DeviceId { get; init; } = string.Empty;
    public string ClientUuid { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public int MonitorIndex { get; init; }

    /// <summary>Path absolut ke file JPEG yang tersimpan sementara di lokal.</summary>
    public string FilePath { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;
    public int RetryCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
