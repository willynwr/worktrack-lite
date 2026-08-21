using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkTrack.Core.Net;
using WorkTrack.Core.Security;
using WorkTrack.SessionAgent.Reporting;

// ── Parse args ──────────────────────────────────────────────────────────────
// Service meneruskan --server-url <url> via CreateProcessAsUser
string? serverUrl = null;
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--server-url")
    {
        serverUrl = args[i + 1];
        break;
    }
}

if (string.IsNullOrWhiteSpace(serverUrl))
{
    Console.Error.WriteLine("ERROR: --server-url <url> tidak diberikan. SessionAgent tidak dapat berjalan.");
    return 1;
}

// ── Load credentials dari CredentialStore (ditulis oleh WorkTrack.Service) ──
var credentialStore = new CredentialStore();
var (deviceId, token) = credentialStore.Load();

if (deviceId is null || token is null)
{
    Console.Error.WriteLine("ERROR: Credentials tidak ditemukan. Pastikan WorkTrack.Service sudah register terlebih dahulu.");
    return 1;
}

// ── Setup host (logging, lifetime) ──────────────────────────────────────────
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton(_ =>
        {
            var http = new HttpClient
            {
                BaseAddress = new Uri(serverUrl),
                Timeout     = TimeSpan.FromSeconds(30)
            };
            var client = new ApiClient(http);
            client.SetAuthToken(token);
            return client;
        });
    })
    .Build();

var logger = host.Services.GetRequiredService<ILogger<Reporter>>();
var apiClient = host.Services.GetRequiredService<ApiClient>();

logger.LogInformation("SessionAgent started | device={DeviceId} | server={Server}", deviceId, serverUrl);

// ── Jalankan reporter loop ───────────────────────────────────────────────────
using var cts = new CancellationTokenSource();

// Tangkap Ctrl+C / SIGTERM agar bisa shutdown gracefully
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

var reporter = new Reporter(deviceId, apiClient, logger);
await using (reporter)
{
    await reporter.RunAsync(cts.Token);
}

logger.LogInformation("SessionAgent exiting.");
return 0;
