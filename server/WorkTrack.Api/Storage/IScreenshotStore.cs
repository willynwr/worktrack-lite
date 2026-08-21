namespace WorkTrack.Api.Storage;

/// <summary>
/// Abstraksi penyimpanan screenshot.
/// MVP: LocalFileScreenshotStore (filesystem lokal).
/// Next: S3CompatibleScreenshotStore — tanpa refactor endpoint/entity.
/// </summary>
public interface IScreenshotStore
{
    /// <summary>
    /// Simpan screenshot dan kembalikan storage path (relatif) yang dicatat di DB.
    /// Format path: {device_id}/{yyyy-MM-dd}/{yyyyMMddHHmmss}_{monitor}.{ext}
    /// </summary>
    Task<string> SaveAsync(
        string deviceId,
        DateTimeOffset timestamp,
        int monitorIndex,
        Stream imageStream,
        string contentType,
        CancellationToken ct = default);

    /// <summary>Baca stream file dari storage. Kembalikan null bila tidak ditemukan.</summary>
    Task<Stream?> GetAsync(string storagePath, CancellationToken ct = default);

    /// <summary>Hapus file dari storage (untuk retention policy di Phase 6).</summary>
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
}
