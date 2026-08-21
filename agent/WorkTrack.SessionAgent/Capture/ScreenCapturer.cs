namespace WorkTrack.SessionAgent.Capture;

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

/// <summary>
/// Mengambil screenshot layar menggunakan GDI BitBlt (fallback resmi Windows).
///
/// Utama  : Windows.Graphics.Capture (Phase 3+, membutuhkan WinRT interop)
/// Fallback: BitBlt via Graphics.CopyFromScreen — dipakai saat ini untuk simplicity MVP.
///
/// Teknik ini TERLIHAT di Task Manager dan tidak menggunakan injection atau evasion apapun.
/// Monitor_index 0 = virtual screen (semua monitor digabung).
/// </summary>
[SupportedOSPlatform("windows")]
public static class ScreenCapturer
{
    // Virtual screen metrics (multi-monitor support)
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_XVIRTUALSCREEN  = 76;
    private const int SM_YVIRTUALSCREEN  = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    /// <summary>
    /// Ambil screenshot seluruh layar virtual (semua monitor) dan encode ke JPEG.
    /// Kembalikan null bila tidak di Windows atau bila terjadi error.
    /// </summary>
    /// <param name="quality">JPEG quality 1–100. Default 85 (balance ukuran vs kualitas).</param>
    public static byte[]? CaptureAsJpeg(int quality = 85)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            var left   = GetSystemMetrics(SM_XVIRTUALSCREEN);
            var top    = GetSystemMetrics(SM_YVIRTUALSCREEN);
            var width  = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            var height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            if (width <= 0 || height <= 0)
                return null;

            using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(left, top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
            }

            return EncodeJpeg(bitmap, quality);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] EncodeJpeg(Bitmap bitmap, int quality)
    {
        var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);

        var jpegCodec = ImageCodecInfo.GetImageEncoders()
            .First(c => c.FormatID == ImageFormat.Jpeg.Guid);

        using var ms = new MemoryStream();
        bitmap.Save(ms, jpegCodec, encoderParams);
        return ms.ToArray();
    }
}
