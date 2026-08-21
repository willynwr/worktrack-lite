using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using WorkTrack.Api.Auth;
using WorkTrack.Api.Data;
using WorkTrack.Api.Endpoints;
using WorkTrack.Api.Services;
using WorkTrack.Api.Storage;

var builder = WebApplication.CreateBuilder(args);

// ── Database: EF Core + MySQL (Pomelo) ────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' tidak ditemukan di appsettings.json.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure(maxRetryCount: 3)
    )
);

// ── Screenshot Storage ────────────────────────────────────────────────────────
builder.Services.AddSingleton<IScreenshotStore, LocalFileScreenshotStore>();

// ── Admin Auth ────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<JwtService>();

// ── Audit Log ─────────────────────────────────────────────────────────────────
builder.Services.AddScoped<AuditLogService>();

// ── CORS: allow Next.js dashboard ─────────────────────────────────────────────
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:3000", "http://127.0.0.1:3000")
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));          // AllowCredentials supaya cookie ter-set dari browser

// ── Rate Limiting ────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    // /devices/register → max 5 per menit per IP
    options.AddFixedWindowLimiter("register", cfg =>
    {
        cfg.PermitLimit        = 5;
        cfg.Window             = TimeSpan.FromMinutes(1);
        cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        cfg.QueueLimit         = 0;
    });

    // /admin/login → max 10 per menit per IP (brute-force protection)
    options.AddFixedWindowLimiter("admin_login", cfg =>
    {
        cfg.PermitLimit        = 10;
        cfg.Window             = TimeSpan.FromMinutes(1);
        cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        cfg.QueueLimit         = 0;
    });
});

// ── Screenshot Retention (Background Service) ─────────────────────────────────
builder.Services.AddHostedService<RetentionService>();

// ── OpenAPI ───────────────────────────────────────────────────────────────────
builder.Services.AddOpenApi();

var app = builder.Build();

// ── Auto migrate on startup (development only) ────────────────────────────────
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    app.MapOpenApi();
}

// ── Middleware ────────────────────────────────────────────────────────────────
app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseMiddleware<DeviceAuthMiddleware>();

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapDeviceEndpoints();
app.MapReportEndpoints();
app.MapScreenshotEndpoints();
app.MapDashboardEndpoints();
app.MapAdminEndpoints();

app.Run();
