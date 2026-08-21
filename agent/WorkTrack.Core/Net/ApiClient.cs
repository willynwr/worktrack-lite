namespace WorkTrack.Core.Net;

using System.Net.Http.Json;
using WorkTrack.Core.Models;

/// <summary>
/// HTTP client wrapper untuk komunikasi agent → WorkTrack.Api.
/// Thread-safe; dipakai sebagai singleton.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;

    public ApiClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>Set Bearer token untuk request selanjutnya (setelah register).</summary>
    public void SetAuthToken(string token)
    {
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Register
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Daftarkan device ke server. Idempotent — server tidak duplikat bila machine_key sudah ada.
    /// Retry 3x dengan exponential backoff.
    /// </summary>
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        Exception? lastEx = null;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("/api/v1/devices/register", request, ct);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<RegisterResponse>(ct);
                    return result ?? throw new InvalidOperationException("Server returned empty response.");
                }

                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex) when (attempt < 2)
            {
                lastEx = ex;
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
        }

        throw new InvalidOperationException("Registration failed after 3 attempts.", lastEx);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Heartbeat
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>Kirim heartbeat ke server. Lempar exception bila gagal (caller handle retry).</summary>
    public async Task<HeartbeatResponse> HeartbeatAsync(HeartbeatRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/v1/devices/heartbeat", request, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<HeartbeatResponse>(ct);
        return result ?? new HeartbeatResponse { Status = "ok", ServerTime = DateTimeOffset.UtcNow };
    }

    // ──────────────────────────────────────────────────────────────────────
    // Report
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Kirim activity report (active_app, idle, uptime) ke server.
    /// Server idempotent by (device_id, client_uuid) — aman untuk retry.
    /// </summary>
    public async Task<ReportResponse> SubmitReportAsync(ReportRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/v1/reports", request, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ReportResponse>(ct);
        return result ?? new ReportResponse { Accepted = true };
    }

    // ──────────────────────────────────────────────────────────────────────
    // Screenshot
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Upload screenshot sebagai multipart/form-data.
    /// Referensikan client_uuid yang sama dengan report agar bisa di-link.
    /// Server idempotent by (device_id, client_uuid, monitor_index).
    /// </summary>
    public async Task UploadScreenshotAsync(
        string deviceId,
        string clientUuid,
        DateTimeOffset timestamp,
        int monitorIndex,
        byte[] imageData,
        string contentType,
        CancellationToken ct = default)
    {
        var ext = contentType.Contains("webp", StringComparison.OrdinalIgnoreCase) ? "webp" : "jpg";

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(deviceId),                       "device_id");
        form.Add(new StringContent(clientUuid),                     "client_uuid");
        form.Add(new StringContent(timestamp.ToString("O")),        "timestamp");
        form.Add(new StringContent(monitorIndex.ToString()),        "monitor_index");

        var fileContent = new ByteArrayContent(imageData);
        fileContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", $"screenshot_{monitorIndex}.{ext}");

        var response = await _http.PostAsync("/api/v1/screenshots", form, ct);
        response.EnsureSuccessStatusCode();
    }
}
