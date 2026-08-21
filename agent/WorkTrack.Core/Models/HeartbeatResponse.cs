namespace WorkTrack.Core.Models;

public class HeartbeatResponse
{
    public string Status { get; set; } = "ok";
    public DateTimeOffset ServerTime { get; set; }
}
