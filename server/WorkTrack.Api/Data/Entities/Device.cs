namespace WorkTrack.Api.Data.Entities;

/// <summary>EF Core entity untuk tabel `devices` di MySQL.</summary>
public class Device
{
    /// <summary>Server-generated, mis. "PC-001". Primary key.</summary>
    public string DeviceId { get; set; } = string.Empty;

    public string Hostname { get; set; } = string.Empty;
    public string WindowsVersion { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;

    /// <summary>Local/private IP — informasi saja, bukan identitas.</summary>
    public string? LocalIp { get; set; }

    /// <summary>SHA-256 hash dari MachineGuid — kunci anti-duplikat registrasi.</summary>
    public string MachineKey { get; set; } = string.Empty;

    /// <summary>SHA-256 hash dari device token (bukan plaintext).</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset RegisteredAt { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }

    /// <summary>Admin dapat menonaktifkan device via PATCH.</summary>
    public bool IsActive { get; set; } = true;
}
