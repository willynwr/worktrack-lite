namespace WorkTrack.SessionAgent.Idle;

using System.Runtime.InteropServices;

/// <summary>
/// Mendeteksi berapa detik pengguna idle (tidak ada input keyboard/mouse).
/// Menggunakan GetLastInputInfo — TANPA keyboard hook, TANPA menyimpan isi ketikan.
/// </summary>
public static class IdleMonitor
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime; // GetTickCount value saat input terakhir
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    /// <summary>
    /// Kembalikan jumlah detik sejak input terakhir (keyboard atau mouse).
    /// Kembalikan 0 bila tidak dapat dideteksi (non-Windows).
    /// </summary>
    public static int GetIdleSeconds()
    {
        if (!OperatingSystem.IsWindows())
            return 0;

        try
        {
            var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            if (!GetLastInputInfo(ref info))
                return 0;

            // Hitung selisih waktu (ms) antara sekarang dan input terakhir
            var idleMs = unchecked((uint)Environment.TickCount - info.dwTime);
            return (int)(idleMs / 1000);
        }
        catch
        {
            return 0;
        }
    }
}
