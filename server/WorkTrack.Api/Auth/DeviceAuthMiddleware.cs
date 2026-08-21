namespace WorkTrack.Api.Auth;

using Microsoft.EntityFrameworkCore;
using WorkTrack.Api.Data;

/// <summary>
/// Middleware yang membaca "Authorization: Bearer {token}" dan memvalidasi ke DB.
/// Bila valid, set HttpContext.Items["DeviceId"] agar endpoint bisa membaca identitas device.
/// </summary>
public class DeviceAuthMiddleware
{
    private readonly RequestDelegate _next;

    public DeviceAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        var authHeader = context.Request.Headers.Authorization.ToString();

        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader[7..].Trim();

            if (!string.IsNullOrEmpty(token))
            {
                var tokenHash = TokenService.HashToken(token);

                var device = await db.Devices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.TokenHash == tokenHash && d.IsActive);

                if (device is not null)
                {
                    context.Items["DeviceId"] = device.DeviceId;
                    context.Items["Device"]   = device;
                }
            }
        }

        await _next(context);
    }
}
