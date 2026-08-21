namespace WorkTrack.SessionAgent.Foreground;

using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>
/// Mendeteksi aplikasi yang sedang aktif di foreground.
/// Hanya menyimpan nama executable (mis. "chrome.exe") — BUKAN window title.
/// Menggunakan GetForegroundWindow → GetWindowThreadProcessId → Process.ProcessName.
/// </summary>
public static class AppMonitor
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>
    /// Kembalikan nama executable foreground (mis. "chrome.exe").
    /// Kembalikan null bila tidak ada window aktif atau tidak dapat diakses.
    /// </summary>
    public static string? GetForegroundExeName()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return null;

            GetWindowThreadProcessId(hwnd, out var processId);
            if (processId == 0)
                return null;

            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName + ".exe";
        }
        catch
        {
            return null;
        }
    }
}
