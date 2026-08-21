namespace WorkTrack.Service;

using System.Runtime.InteropServices;
using WorkTrack.Core.Config;

/// <summary>
/// Meluncurkan WorkTrack.SessionAgent di sesi interaktif pengguna (Session 1+).
///
/// MENGAPA DIPERLUKAN: Windows Service berjalan di Session 0 (non-interaktif) dan
/// tidak dapat menangkap layar pengguna. Screenshot HARUS diambil dari proses yang
/// berjalan di sesi interaktif. Ini adalah teknik resmi Windows, BUKAN stealth.
///
/// Ref: https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsqueryusertoken
/// </summary>
public class SessionLauncher
{
    // ── P/Invoke Declarations ───────────────────────────────────────────────

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr phToken);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessAsUser(
        IntPtr hToken,
        string? lpApplicationName,
        string? lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(
        out IntPtr lpEnvironment, IntPtr hToken, bool bInherit);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public uint cb;
        public string? lpReserved, lpDesktop, lpTitle;
        public uint dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars;
        public uint dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public uint dwProcessId, dwThreadId;
    }

    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const uint NORMAL_PRIORITY_CLASS      = 0x00000020;
    private const uint CREATE_NO_WINDOW           = 0x08000000;

    // ── Implementation ──────────────────────────────────────────────────────

    private readonly ILogger<SessionLauncher> _logger;
    private readonly string _agentExePath;
    private readonly AgentConfig _config;

    public SessionLauncher(ILogger<SessionLauncher> logger, AgentConfig config)
    {
        _logger = logger;
        _config = config;

        // SessionAgent.exe berada di direktori yang sama dengan Service.exe
        var baseDir = AppContext.BaseDirectory;
        _agentExePath = Path.Combine(baseDir, "WorkTrack.SessionAgent.exe");
    }

    /// <summary>
    /// Luncurkan SessionAgent di sesi interaktif aktif.
    /// Safe untuk dipanggil di non-Windows (no-op dengan log warning).
    /// </summary>
    public void LaunchInUserSession()
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("SessionLauncher: non-Windows OS — SessionAgent tidak diluncurkan.");
            return;
        }

        if (!File.Exists(_agentExePath))
        {
            _logger.LogError("SessionAgent exe tidak ditemukan: {Path}", _agentExePath);
            return;
        }

        try
        {
            DoLaunch();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gagal meluncurkan SessionAgent.");
        }
    }

    private void DoLaunch()
    {
        // Dapatkan session ID sesi konsol aktif (user yang sedang login)
        var sessionId = WTSGetActiveConsoleSessionId();
        if (sessionId == 0xFFFFFFFF)
        {
            _logger.LogWarning("Tidak ada sesi konsol aktif — SessionAgent tidak diluncurkan.");
            return;
        }

        // Dapatkan token user dari sesi tersebut
        if (!WTSQueryUserToken(sessionId, out var userToken))
        {
            _logger.LogError("WTSQueryUserToken gagal: Win32 error {Error}", Marshal.GetLastWin32Error());
            return;
        }

        try
        {
            // Buat environment block untuk user (agar env vars seperti APPDATA ter-set dengan benar)
            CreateEnvironmentBlock(out var envBlock, userToken, false);

            try
            {
                var si = new STARTUPINFO { cb = (uint)Marshal.SizeOf<STARTUPINFO>() };
                // Server URL dikirim sebagai command line arg (bukan rahasia)
                var commandLine = $"\"{_agentExePath}\" --server-url \"{_config.ServerUrl}\"";

                var ok = CreateProcessAsUser(
                    hToken:               userToken,
                    lpApplicationName:    null,
                    lpCommandLine:        commandLine,
                    lpProcessAttributes:  IntPtr.Zero,
                    lpThreadAttributes:   IntPtr.Zero,
                    bInheritHandles:      false,
                    dwCreationFlags:      CREATE_UNICODE_ENVIRONMENT | NORMAL_PRIORITY_CLASS | CREATE_NO_WINDOW,
                    lpEnvironment:        envBlock,
                    lpCurrentDirectory:   Path.GetDirectoryName(_agentExePath),
                    lpStartupInfo:        ref si,
                    lpProcessInformation: out var pi);

                if (!ok)
                {
                    _logger.LogError("CreateProcessAsUser gagal: Win32 error {Error}", Marshal.GetLastWin32Error());
                    return;
                }

                _logger.LogInformation(
                    "SessionAgent diluncurkan di Session {Session} | PID {PID}",
                    sessionId, pi.dwProcessId);

                CloseHandle(pi.hProcess);
                CloseHandle(pi.hThread);
            }
            finally
            {
                if (envBlock != IntPtr.Zero)
                    DestroyEnvironmentBlock(envBlock);
            }
        }
        finally
        {
            CloseHandle(userToken);
        }
    }
}
