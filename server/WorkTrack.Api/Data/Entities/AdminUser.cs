namespace WorkTrack.Api.Data.Entities;

/// <summary>Admin user yang dapat login ke Web Dashboard.</summary>
public class AdminUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;

    /// <summary>BCrypt hash dari password (bukan plaintext).</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}
