namespace WorkTrack.Core.Config;

public class AgentConfig
{
    /// <summary>URL server WorkTrack.Api, mis. https://192.168.1.10:7000</summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>Versi agent — diisi dari assembly atau config.</summary>
    public string AgentVersion { get; set; } = "1.0.0";
}
