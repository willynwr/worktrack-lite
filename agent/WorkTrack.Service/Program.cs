using WorkTrack.Core.Config;
using WorkTrack.Core.Net;
using WorkTrack.Core.Security;
using WorkTrack.Service;

var builder = Host.CreateApplicationBuilder(args);

// ── Configuration ──────────────────────────────────
var agentConfig = builder.Configuration.GetSection("Agent").Get<AgentConfig>()
    ?? throw new InvalidOperationException("Bagian 'Agent' tidak ditemukan di appsettings.json.");

builder.Services.AddSingleton(agentConfig);

// ── HTTP Client ─────────────────────────────────────
builder.Services.AddSingleton(_ =>
    new ApiClient(new HttpClient
    {
        BaseAddress = new Uri(agentConfig.ServerUrl),
        Timeout     = TimeSpan.FromSeconds(30)
    }));

// ── Security ────────────────────────────────────────
builder.Services.AddSingleton<CredentialStore>();

// ── Session Launcher ────────────────────────────────
builder.Services.AddSingleton<SessionLauncher>();

// ── Worker ──────────────────────────────────────────
builder.Services.AddHostedService<ServiceWorker>();

// ── Windows Service ─────────────────────────────────
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "WorkTrack Agent";
});

var host = builder.Build();
await host.RunAsync();
