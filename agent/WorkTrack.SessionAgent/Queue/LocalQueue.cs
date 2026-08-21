namespace WorkTrack.SessionAgent.Queue;

using System.Text.Json;
using Microsoft.Data.Sqlite;
using WorkTrack.Core.Models;

/// <summary>
/// Offline queue berbasis SQLite embedded (agent-side, bukan server).
///
/// Lokasi:
///   DB   : %ProgramData%\WorkTrack\queue.db
///   Files: %ProgramData%\WorkTrack\queue_screenshots\{uuid}_{monitor}.jpg
///
/// Flow:
///   Upload gagal → enqueue → internet kembali → flush queue → server idempotent → hapus dari queue
///   Max retry: 48 kali (~48 menit @ 60s) → drop otomatis agar queue tidak membengkak.
///
/// Thread-safety: Reporter berjalan dalam satu async loop sequential, tidak perlu locking.
/// </summary>
public sealed class LocalQueue : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly string _screenshotDir;

    /// <summary>Setelah MaxRetries kali gagal, item dihapus dari queue.</summary>
    private const int MaxRetries = 48;

    public LocalQueue()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "WorkTrack");
        Directory.CreateDirectory(dir);

        _screenshotDir = Path.Combine(dir, "queue_screenshots");
        Directory.CreateDirectory(_screenshotDir);

        _conn = new SqliteConnection($"Data Source={Path.Combine(dir, "queue.db")}");
        _conn.Open();

        InitSchema();
    }

    private void InitSchema()
    {
        Exec("""
            PRAGMA journal_mode=WAL;

            CREATE TABLE IF NOT EXISTS queued_reports (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                payload     TEXT    NOT NULL,
                retry_count INTEGER NOT NULL DEFAULT 0,
                created_at  TEXT    NOT NULL
            );

            CREATE TABLE IF NOT EXISTS queued_screenshots (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                device_id     TEXT    NOT NULL,
                client_uuid   TEXT    NOT NULL,
                timestamp     TEXT    NOT NULL,
                monitor_index INTEGER NOT NULL DEFAULT 0,
                file_path     TEXT    NOT NULL,
                content_type  TEXT    NOT NULL,
                retry_count   INTEGER NOT NULL DEFAULT 0,
                created_at    TEXT    NOT NULL
            );
        """);
    }

    // ── Enqueue ──────────────────────────────────────────────────────────────

    /// <summary>Tambahkan report ke queue untuk dikirim ulang saat online.</summary>
    public void EnqueueReport(ReportRequest report)
    {
        Exec(
            "INSERT INTO queued_reports (payload, created_at) VALUES (@p, @t)",
            ("@p", JsonSerializer.Serialize(report)),
            ("@t", DateTimeOffset.UtcNow.ToString("O")));
    }

    /// <summary>Simpan screenshot ke file lokal dan tambahkan ke queue.</summary>
    public void EnqueueScreenshot(
        string deviceId, string clientUuid, DateTimeOffset timestamp,
        int monitorIndex, byte[] imageData, string contentType)
    {
        var ext      = contentType.Contains("webp", StringComparison.OrdinalIgnoreCase) ? "webp" : "jpg";
        var fileName = $"{clientUuid}_{monitorIndex}.{ext}";
        var filePath = Path.Combine(_screenshotDir, fileName);

        File.WriteAllBytes(filePath, imageData);

        Exec("""
            INSERT INTO queued_screenshots
                (device_id, client_uuid, timestamp, monitor_index, file_path, content_type, created_at)
            VALUES (@dev, @uuid, @ts, @mon, @fp, @ct, @ca)
            """,
            ("@dev", deviceId), ("@uuid", clientUuid), ("@ts", timestamp.ToString("O")),
            ("@mon", (long)monitorIndex), ("@fp", filePath), ("@ct", contentType),
            ("@ca", DateTimeOffset.UtcNow.ToString("O")));
    }

    // ── Read ─────────────────────────────────────────────────────────────────

    public IReadOnlyList<QueuedReport> GetPendingReports(int limit = 10)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = $"SELECT id, payload, retry_count, created_at FROM queued_reports ORDER BY id LIMIT {limit}";
        using var r = cmd.ExecuteReader();

        var list = new List<QueuedReport>();
        while (r.Read())
        {
            list.Add(new QueuedReport
            {
                Id          = r.GetInt64(0),
                PayloadJson = r.GetString(1),
                RetryCount  = r.GetInt32(2),
                CreatedAt   = DateTimeOffset.Parse(r.GetString(3))
            });
        }
        return list;
    }

    public IReadOnlyList<QueuedScreenshot> GetPendingScreenshots(int limit = 5)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT id, device_id, client_uuid, timestamp, monitor_index,
                   file_path, content_type, retry_count, created_at
            FROM queued_screenshots ORDER BY id LIMIT {limit}
            """;
        using var r = cmd.ExecuteReader();

        var list = new List<QueuedScreenshot>();
        while (r.Read())
        {
            list.Add(new QueuedScreenshot
            {
                Id           = r.GetInt64(0),
                DeviceId     = r.GetString(1),
                ClientUuid   = r.GetString(2),
                Timestamp    = DateTimeOffset.Parse(r.GetString(3)),
                MonitorIndex = r.GetInt32(4),
                FilePath     = r.GetString(5),
                ContentType  = r.GetString(6),
                RetryCount   = r.GetInt32(7),
                CreatedAt    = DateTimeOffset.Parse(r.GetString(8))
            });
        }
        return list;
    }

    /// <summary>Jumlah total item pending (report + screenshot) di queue.</summary>
    public int GetPendingCount()
    {
        var r = (long)(Scalar("SELECT COUNT(*) FROM queued_reports") ?? 0L);
        var s = (long)(Scalar("SELECT COUNT(*) FROM queued_screenshots") ?? 0L);
        return (int)(r + s);
    }

    // ── Mark done ────────────────────────────────────────────────────────────

    public void MarkReportSent(long id)
        => Exec("DELETE FROM queued_reports WHERE id = @id", ("@id", id));

    public void MarkScreenshotSent(long id, string filePath)
    {
        Exec("DELETE FROM queued_screenshots WHERE id = @id", ("@id", id));
        TryDeleteFile(filePath);
    }

    // ── Increment retry / expire ──────────────────────────────────────────────

    public void IncrementReportRetry(long id)
    {
        var count = (long)(Scalar("SELECT retry_count FROM queued_reports WHERE id = @id", ("@id", id)) ?? 0L);
        if (count >= MaxRetries)
            Exec("DELETE FROM queued_reports WHERE id = @id", ("@id", id));
        else
            Exec("UPDATE queued_reports SET retry_count = retry_count + 1 WHERE id = @id", ("@id", id));
    }

    public void IncrementScreenshotRetry(long id, string filePath)
    {
        var count = (long)(Scalar("SELECT retry_count FROM queued_screenshots WHERE id = @id", ("@id", id)) ?? 0L);
        if (count >= MaxRetries)
        {
            Exec("DELETE FROM queued_screenshots WHERE id = @id", ("@id", id));
            TryDeleteFile(filePath);
        }
        else
        {
            Exec("UPDATE queued_screenshots SET retry_count = retry_count + 1 WHERE id = @id", ("@id", id));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void Exec(string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }

    private object? Scalar(string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        return cmd.ExecuteScalar();
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* ignore cleanup errors */ }
    }

    public void Dispose() => _conn.Dispose();
}
