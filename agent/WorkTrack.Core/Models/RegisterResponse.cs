namespace WorkTrack.Core.Models;

public class RegisterResponse
{
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Token plaintext — dikembalikan SEKALI saat registrasi pertama.
    /// Null jika device sudah terdaftar (already_registered).
    /// </summary>
    public string? DeviceToken { get; set; }

    /// <summary>"registered" | "already_registered"</summary>
    public string Status { get; set; } = string.Empty;

    public DateTimeOffset ServerTime { get; set; }
}
