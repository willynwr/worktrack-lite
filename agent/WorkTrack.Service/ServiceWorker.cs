namespace WorkTrack.Service;

using WorkTrack.Core.Config;
using WorkTrack.Core.Models;
using WorkTrack.Core.Net;
using WorkTrack.Core.Security;

/// <summary>
/// Background worker utama WorkTrack Agent (Windows Service).
///
/// Flow:
///   1. Cek apakah device sudah terdaftar (credentials via DPAPI).
///   2. Bila belum → register ke API → simpan token.
///   3. Luncurkan SessionAgent di sesi interaktif pengguna.
///   4. Loop heartbeat setiap 60 detik.
/// </summary>
public class ServiceWorker : BackgroundService
{
    private readonly ILogger<ServiceWorker> _logger;
    private readonly AgentConfig _config;
    private readonly ApiClient _apiClient;
    private readonly CredentialStore _credentialStore;
    private readonly SessionLauncher _sessionLauncher;

    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(60);

    private string? _deviceId;

    public ServiceWorker(
        ILogger<ServiceWorker> logger,
        AgentConfig config,
        ApiClient apiClient,
        CredentialStore credentialStore,
        SessionLauncher sessionLauncher)
    {
        _logger = logger;
        _config = config;
        _apiClient = apiClient;
        _credentialStore = credentialStore;
        _sessionLauncher = sessionLauncher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WorkTrack Agent starting (v{Version})", _config.AgentVersion);

        // ── Step 1: Pastikan device terdaftar ──
        try
        {
            await EnsureRegisteredAsync(stoppingToken);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed. Agent will stop.");
            return;
        }

        // ── Step 2: Luncurkan SessionAgent di sesi interaktif ──
        _sessionLauncher.LaunchInUserSession();

        // ── Step 3: Heartbeat loop ──
        _logger.LogInformation("Heartbeat loop started (interval: {Interval}s)", (int)HeartbeatInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendHeartbeatAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Heartbeat failed — will retry in {Interval}s", (int)HeartbeatInterval.TotalSeconds);
            }

            // Self-healing: relaunch SessionAgent bila belum/tidak jalan — misal service
            // start sebelum ada sesi user login saat boot, atau SessionAgent crash.
            if (!_sessionLauncher.IsSessionAgentRunning())
            {
                _logger.LogWarning("SessionAgent tidak terdeteksi jalan — mencoba meluncurkan ulang.");
                _sessionLauncher.LaunchInUserSession();
            }

            try { await Task.Delay(HeartbeatInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("WorkTrack Agent stopped.");
    }

    private async Task EnsureRegisteredAsync(CancellationToken ct)
    {
        var (existingDeviceId, existingToken) = _credentialStore.Load();

        if (existingDeviceId is not null && existingToken is not null)
        {
            _logger.LogInformation("Device already registered: {DeviceId}", existingDeviceId);
            _deviceId = existingDeviceId;
            _apiClient.SetAuthToken(existingToken);
            return;
        }

        _logger.LogInformation("No credentials found — registering device...");

        var request = new RegisterRequest
        {
            MachineKey     = MachineKeyHelper.GetMachineKey(),
            Hostname       = Environment.MachineName,
            WindowsVersion = Environment.OSVersion.ToString(),
            AgentVersion   = _config.AgentVersion,
            LocalIp        = GetLocalIp()
        };

        var response = await _apiClient.RegisterAsync(request, ct);

        if (response.DeviceToken is null)
        {
            throw new InvalidOperationException(
                $"Device {response.DeviceId} sudah terdaftar di server tapi tidak ada credentials lokal. " +
                "Minta admin untuk reset device token.");
        }

        _credentialStore.Save(response.DeviceId, response.DeviceToken);
        _apiClient.SetAuthToken(response.DeviceToken);
        _deviceId = response.DeviceId;

        _logger.LogInformation("Registration successful: {DeviceId}", _deviceId);
    }

    private async Task SendHeartbeatAsync(CancellationToken ct)
    {
        if (_deviceId is null) return;

        await _apiClient.HeartbeatAsync(new HeartbeatRequest
        {
            DeviceId      = _deviceId,
            LocalIp       = GetLocalIp(),
            UptimeSeconds = GetUptimeSeconds()
        }, ct);

        _logger.LogInformation("[{DeviceId}] Heartbeat OK", _deviceId);
    }

    private static string? GetLocalIp()
    {
        try
        {
            return System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName())
                .AddressList
                .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                ?.ToString();
        }
        catch { return null; }
    }

    private static long GetUptimeSeconds() =>
        (long)TimeSpan.FromMilliseconds(Environment.TickCount64).TotalSeconds;
}
