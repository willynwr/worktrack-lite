namespace WorkTrack.Core.Security;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// Menyimpan dan memuat device credentials (device_id + token) secara aman.
/// Dipakai oleh WorkTrack.Service (write) dan WorkTrack.SessionAgent (read).
///
/// Windows  : token di-encrypt dengan DPAPI (ProtectedData, scope LocalMachine).
/// Non-Windows : fallback plaintext — HANYA untuk development/testing.
/// </summary>
public class CredentialStore
{
    private readonly string _storePath;

    private sealed record StoredCredentials(string DeviceId, string TokenData, bool IsEncrypted);

    public CredentialStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var dir = Path.Combine(appData, "WorkTrack");
        Directory.CreateDirectory(dir);
        _storePath = Path.Combine(dir, "credentials.dat");
    }

    /// <summary>Simpan device_id dan token (token di-encrypt bila Windows).</summary>
    public void Save(string deviceId, string token)
    {
        StoredCredentials stored;

        if (OperatingSystem.IsWindows())
        {
            var tokenBytes = Encoding.UTF8.GetBytes(token);
            var encrypted = ProtectedData.Protect(tokenBytes, null, DataProtectionScope.LocalMachine);
            stored = new StoredCredentials(deviceId, Convert.ToBase64String(encrypted), IsEncrypted: true);
        }
        else
        {
            // Development fallback — plaintext, non-Windows only
            stored = new StoredCredentials(deviceId, token, IsEncrypted: false);
        }

        File.WriteAllText(_storePath, JsonSerializer.Serialize(stored));
    }

    /// <summary>Muat credentials. Kembalikan (null, null) bila belum ada atau error.</summary>
    public (string? DeviceId, string? Token) Load()
    {
        if (!File.Exists(_storePath))
            return (null, null);

        try
        {
            var json = File.ReadAllText(_storePath);
            var stored = JsonSerializer.Deserialize<StoredCredentials>(json);
            if (stored is null) return (null, null);

            string token;
            if (stored.IsEncrypted && OperatingSystem.IsWindows())
            {
                var encryptedBytes = Convert.FromBase64String(stored.TokenData);
                var tokenBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.LocalMachine);
                token = Encoding.UTF8.GetString(tokenBytes);
            }
            else
            {
                token = stored.TokenData;
            }

            return (stored.DeviceId, token);
        }
        catch
        {
            return (null, null);
        }
    }

    /// <summary>Hapus credentials (admin reset / testing).</summary>
    public void Clear()
    {
        if (File.Exists(_storePath))
            File.Delete(_storePath);
    }
}
