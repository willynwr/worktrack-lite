namespace WorkTrack.Api.Services;

using Microsoft.EntityFrameworkCore;
using WorkTrack.Api.Data;
using WorkTrack.Api.Storage;

/// <summary>
/// Background service yang berjalan sekali per hari dan menghapus screenshot
/// lebih lama dari RetentionDays (default 30 hari) dari DB dan filesystem.
///
/// Konfigurasi di appsettings.json: { "Admin": { "RetentionDays": 30 } }
/// </summary>
public class RetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScreenshotStore _store;
    private readonly ILogger<RetentionService> _logger;
    private readonly int _retentionDays;

    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    public RetentionService(
        IServiceScopeFactory scopeFactory,
        IScreenshotStore store,
        IConfiguration config,
        ILogger<RetentionService> logger)
    {
        _scopeFactory   = scopeFactory;
        _store          = store;
        _logger         = logger;
        _retentionDays  = int.Parse(config["Admin:RetentionDays"] ?? "30");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Retention] Service started. Retention: {Days} hari", _retentionDays);

        // Jalankan sekali saat startup agar cleanup terjadi walau server lama tidak jalan
        await RunCleanupAsync(stoppingToken);

        // Lalu setiap 24 jam
        using var timer = new PeriodicTimer(RunInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCleanupAsync(stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-_retentionDays);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var old = await db.Screenshots
                .Where(s => s.CreatedAt < cutoff)
                .ToListAsync(ct);

            if (old.Count == 0)
            {
                _logger.LogDebug("[Retention] Tidak ada screenshot expired.");
                return;
            }

            _logger.LogInformation("[Retention] Menghapus {Count} screenshot (cutoff: {Cutoff:O})", old.Count, cutoff);

            foreach (var ss in old)
            {
                await _store.DeleteAsync(ss.StoragePath, ct);
            }

            db.Screenshots.RemoveRange(old);
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("[Retention] Selesai hapus {Count} screenshot", old.Count);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "[Retention] Error saat cleanup");
        }
    }
}
