namespace WorkTrack.SessionAgent.Reporting;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using WorkTrack.Core.Models;
using WorkTrack.Core.Net;
using WorkTrack.SessionAgent.Capture;
using WorkTrack.SessionAgent.Foreground;
using WorkTrack.SessionAgent.Idle;
using WorkTrack.SessionAgent.Queue;
using WorkTrack.SessionAgent.Uptime;

/// <summary>
/// Mengumpulkan data setiap 60 detik, mengirim report + screenshot ke WorkTrack.Api.
/// Bila upload gagal (offline) → masukkan ke LocalQueue → retry saat online kembali.
///
/// Flow per siklus:
///   1. Flush pending queue (max 10 report + 5 screenshot per siklus)
///   2. Snapshot: active app + idle + uptime
///   3. Capture screenshot (JPEG)
///   4. POST /api/v1/reports      → gagal: enqueue report
///   5. POST /api/v1/screenshots  → gagal: enqueue screenshot ke file lokal
/// </summary>
public class Reporter : IAsyncDisposable
{
    private readonly string _deviceId;
    private readonly ApiClient _apiClient;
    private readonly ILogger<Reporter> _logger;
    private readonly LocalQueue _queue;

    private static readonly TimeSpan ReportInterval = TimeSpan.FromSeconds(60);

    public Reporter(string deviceId, ApiClient apiClient, ILogger<Reporter> logger)
    {
        _deviceId  = deviceId;
        _apiClient = apiClient;
        _logger    = logger;
        _queue     = new LocalQueue();

        var pending = _queue.GetPendingCount();
        if (pending > 0)
            _logger.LogInformation("[Queue] {Count} item pending dari sesi sebelumnya", pending);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("[Reporter] Started for device {DeviceId}", _deviceId);

        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(ReportInterval, ct); }
            catch (OperationCanceledException) { break; }

            await CollectAndSubmitAsync(ct);
        }

        _logger.LogInformation("[Reporter] Stopped.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Main cycle
    // ──────────────────────────────────────────────────────────────────────────

    private async Task CollectAndSubmitAsync(CancellationToken ct)
    {
        // ── 1. Flush pending queue DULU (agar backlog hilang sebelum data baru masuk) ──
        await FlushQueueAsync(ct);

        if (ct.IsCancellationRequested) return;

        // ── 2. Snapshot ──
        var timestamp  = DateTimeOffset.UtcNow;
        var clientUuid = Guid.NewGuid().ToString();
        var activeApp  = AppMonitor.GetForegroundExeName();
        var idleSec    = IdleMonitor.GetIdleSeconds();
        var uptimeSec  = UptimeMonitor.GetUptimeSeconds();

        // ── 3. Capture screenshot ──
        byte[]? screenshotBytes = null;
        if (OperatingSystem.IsWindows())
        {
            screenshotBytes = ScreenCapturer.CaptureAsJpeg(quality: 85);
            if (screenshotBytes is null)
                _logger.LogWarning("[{DeviceId}] Screenshot capture returned null", _deviceId);
        }

        _logger.LogInformation(
            "[{DeviceId}] Cycle | app={App} idle={Idle}s uptime={Uptime}s screenshot={SsKb}KB",
            _deviceId, activeApp ?? "none", idleSec, uptimeSec,
            screenshotBytes is not null ? screenshotBytes.Length / 1024 : 0);

        // ── 4. Submit report ──
        await SubmitReportAsync(new ReportRequest
        {
            DeviceId      = _deviceId,
            ClientUuid    = clientUuid,
            Timestamp     = timestamp,
            ActiveApp     = activeApp,
            IdleSeconds   = idleSec,
            UptimeSeconds = uptimeSec
        }, ct);

        // ── 5. Upload screenshot ──
        if (screenshotBytes is not null)
            await UploadScreenshotAsync(clientUuid, timestamp, screenshotBytes, ct);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Submit (dengan queue fallback)
    // ──────────────────────────────────────────────────────────────────────────

    private async Task SubmitReportAsync(ReportRequest request, CancellationToken ct)
    {
        try
        {
            var response = await _apiClient.SubmitReportAsync(request, ct);
            if (response.Duplicate)
                _logger.LogDebug("[{DeviceId}] Report duplicate (uuid={Uuid})", _deviceId, request.ClientUuid[..8]);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "[{DeviceId}] Report upload failed — queued untuk retry", _deviceId);
            _queue.EnqueueReport(request);
        }
    }

    private async Task UploadScreenshotAsync(
        string clientUuid, DateTimeOffset timestamp, byte[] imageData, CancellationToken ct)
    {
        try
        {
            await _apiClient.UploadScreenshotAsync(
                _deviceId, clientUuid, timestamp, monitorIndex: 0,
                imageData, contentType: "image/jpeg", ct);

            _logger.LogInformation("[{DeviceId}] Screenshot uploaded ({Kb}KB)", _deviceId, imageData.Length / 1024);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "[{DeviceId}] Screenshot upload failed — queued untuk retry", _deviceId);
            _queue.EnqueueScreenshot(_deviceId, clientUuid, timestamp, 0, imageData, "image/jpeg");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Queue flush
    // ──────────────────────────────────────────────────────────────────────────

    private async Task FlushQueueAsync(CancellationToken ct)
    {
        var count = _queue.GetPendingCount();
        if (count == 0) return;

        _logger.LogInformation("[Queue] Flushing {Count} pending items...", count);

        // Flush reports (max 10 per siklus agar tidak overload server)
        foreach (var item in _queue.GetPendingReports(limit: 10))
        {
            if (ct.IsCancellationRequested) return;
            try
            {
                var report = JsonSerializer.Deserialize<ReportRequest>(item.PayloadJson)
                    ?? throw new InvalidOperationException("Failed to deserialize queued report.");

                await _apiClient.SubmitReportAsync(report, ct);
                _queue.MarkReportSent(item.Id);
                _logger.LogDebug("[Queue] Report flushed (id={Id}, retry={R})", item.Id, item.RetryCount);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "[Queue] Report retry failed (id={Id})", item.Id);
                _queue.IncrementReportRetry(item.Id);
                return; // Berhenti flush bila server tidak bisa dicapai
            }
        }

        // Flush screenshots (max 5 per siklus — screenshot lebih besar)
        foreach (var item in _queue.GetPendingScreenshots(limit: 5))
        {
            if (ct.IsCancellationRequested) return;
            try
            {
                if (!File.Exists(item.FilePath))
                {
                    // File hilang (mis. disk cleanup) — hapus dari queue
                    _queue.MarkScreenshotSent(item.Id, item.FilePath);
                    continue;
                }

                var data = await File.ReadAllBytesAsync(item.FilePath, ct);
                await _apiClient.UploadScreenshotAsync(
                    item.DeviceId, item.ClientUuid, item.Timestamp,
                    item.MonitorIndex, data, item.ContentType, ct);

                _queue.MarkScreenshotSent(item.Id, item.FilePath);
                _logger.LogDebug("[Queue] Screenshot flushed (id={Id}, retry={R})", item.Id, item.RetryCount);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "[Queue] Screenshot retry failed (id={Id})", item.Id);
                _queue.IncrementScreenshotRetry(item.Id, item.FilePath);
                return;
            }
        }

        _logger.LogInformation("[Queue] Flush complete. Remaining: {Count}", _queue.GetPendingCount());
    }

    public ValueTask DisposeAsync()
    {
        _queue.Dispose();
        return ValueTask.CompletedTask;
    }
}
