namespace WorkTrack.Core.Models;

public class RegisterRequest
{
    /// <summary>SHA-256 hash dari Windows MachineGuid — identitas stabil per PC.</summary>
    public string MachineKey { get; set; } = string.Empty;

    public string Hostname { get; set; } = string.Empty;
    public string WindowsVersion { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;

    /// <summary>Local/private IP — informasi saja, bukan identitas.</summary>
    public string? LocalIp { get; set; }
}
