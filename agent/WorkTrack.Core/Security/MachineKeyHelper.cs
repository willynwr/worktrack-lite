namespace WorkTrack.Core.Security;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Menghasilkan machine_key stabil dari Windows MachineGuid (di-hash SHA-256).
/// MachineGuid berada di HKLM\SOFTWARE\Microsoft\Cryptography — stabil antar reboot.
/// </summary>
public static class MachineKeyHelper
{
    public static string GetMachineKey()
    {
        string? rawGuid = null;

        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine
                    .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                rawGuid = key?.GetValue("MachineGuid")?.ToString();
            }
            catch { }
        }

        // Fallback: kombinasi machine name (untuk dev di non-Windows)
        rawGuid ??= $"{Environment.MachineName}-{Environment.ProcessorCount}-{Environment.OSVersion.Platform}";

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawGuid));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
