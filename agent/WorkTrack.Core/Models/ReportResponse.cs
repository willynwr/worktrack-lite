namespace WorkTrack.Core.Models;

public class ReportResponse
{
    public long ReportId { get; set; }
    public bool Accepted { get; set; }

    /// <summary>True bila record dengan client_uuid ini sudah ada (upload ulang, tidak duplikat).</summary>
    public bool Duplicate { get; set; }
}
