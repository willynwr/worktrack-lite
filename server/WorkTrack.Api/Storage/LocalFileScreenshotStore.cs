namespace WorkTrack.Api.Storage;

/// <summary>
/// Implementasi IScreenshotStore menggunakan filesystem lokal.
/// Path: {BasePath}/{device_id}/{yyyy-MM-dd}/{yyyyMMddHHmmss}_{monitor}.{ext}
///
/// BasePath dikonfigurasi via ScreenshotStore:BasePath di appsettings.json.
/// </summary>
public class LocalFileScreenshotStore : IScreenshotStore
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileScreenshotStore> _logger;

    public LocalFileScreenshotStore(IConfiguration config, ILogger<LocalFileScreenshotStore> logger)
    {
        _basePath = config["ScreenshotStore:BasePath"] ?? "screenshots";
        _logger   = logger;

        // Pastikan base directory ada saat startup
        Directory.CreateDirectory(_basePath);
        _logger.LogInformation("Screenshot store: {Path}", Path.GetFullPath(_basePath));
    }

    public async Task<string> SaveAsync(
        string deviceId,
        DateTimeOffset timestamp,
        int monitorIndex,
        Stream imageStream,
        string contentType,
        CancellationToken ct = default)
    {
        var date     = timestamp.ToString("yyyy-MM-dd");
        var timeStr  = timestamp.ToString("yyyyMMddHHmmss");
        var ext      = contentType.Contains("webp", StringComparison.OrdinalIgnoreCase) ? "webp" : "jpg";
        var filename = $"{timeStr}_{monitorIndex}.{ext}";

        // Relative path disimpan di DB (portable, tidak bergantung pada server path)
        var relativePath = Path.Combine(deviceId, date, filename);
        var fullPath     = Path.Combine(_basePath, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fileStream = File.Create(fullPath);
        await imageStream.CopyToAsync(fileStream, ct);

        return relativePath;
    }

    public Task<Stream?> GetAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, storagePath);

        if (!File.Exists(fullPath))
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(File.OpenRead(fullPath));
    }

    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_basePath, storagePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogDebug("Deleted screenshot: {Path}", storagePath);
        }

        return Task.CompletedTask;
    }
}
