namespace WorkTrack.Api.Data;

using Microsoft.EntityFrameworkCore;
using WorkTrack.Api.Data.Entities;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Device>         Devices         => Set<Device>();
    public DbSet<ActivityReport> ActivityReports => Set<ActivityReport>();
    public DbSet<Screenshot>     Screenshots     => Set<Screenshot>();
    public DbSet<AdminUser>      AdminUsers      => Set<AdminUser>();
    public DbSet<AuditLog>       AuditLogs       => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── Device ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<Device>(e =>
        {
            e.HasKey(d => d.DeviceId);
            e.Property(d => d.DeviceId).HasMaxLength(50);
            e.Property(d => d.Hostname).IsRequired().HasMaxLength(255);
            e.Property(d => d.WindowsVersion).IsRequired().HasMaxLength(255);
            e.Property(d => d.AgentVersion).IsRequired().HasMaxLength(50);
            e.Property(d => d.LocalIp).HasMaxLength(50);
            e.Property(d => d.MachineKey).IsRequired().HasMaxLength(64);
            e.HasIndex(d => d.MachineKey).IsUnique();
            e.Property(d => d.TokenHash).IsRequired().HasMaxLength(64);
            e.Property(d => d.IsActive).HasDefaultValue(true);
        });

        // ── ActivityReport ──────────────────────────────────────────────────────
        modelBuilder.Entity<ActivityReport>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.DeviceId).IsRequired().HasMaxLength(50);
            e.Property(r => r.ClientUuid).IsRequired().HasMaxLength(36);
            e.Property(r => r.ActiveApp).HasMaxLength(255);
            e.HasIndex(r => new { r.DeviceId, r.ClientUuid }).IsUnique();
            e.HasIndex(r => new { r.DeviceId, r.Timestamp });
            e.HasOne(r => r.Device).WithMany().HasForeignKey(r => r.DeviceId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Screenshot ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Screenshot>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.DeviceId).IsRequired().HasMaxLength(50);
            e.Property(s => s.ClientUuid).IsRequired().HasMaxLength(36);
            e.Property(s => s.StoragePath).IsRequired().HasMaxLength(500);
            e.Property(s => s.ContentType).IsRequired().HasMaxLength(50);
            e.HasIndex(s => new { s.DeviceId, s.ClientUuid, s.MonitorIndex }).IsUnique();
            e.HasIndex(s => new { s.DeviceId, s.Timestamp });
            e.HasOne(s => s.Device).WithMany().HasForeignKey(s => s.DeviceId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── AdminUser ───────────────────────────────────────────────────────────
        modelBuilder.Entity<AdminUser>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Username).IsRequired().HasMaxLength(100);
            e.HasIndex(a => a.Username).IsUnique();
            e.Property(a => a.PasswordHash).IsRequired().HasMaxLength(255);
        });

        // ── AuditLog ────────────────────────────────────────────────────────────
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.AdminUsername).IsRequired().HasMaxLength(100);
            e.Property(l => l.Action).IsRequired().HasMaxLength(100);
            e.Property(l => l.Target).HasMaxLength(255);
            e.Property(l => l.IpAddress).HasMaxLength(50);
            e.HasIndex(l => l.Timestamp);
        });
    }
}
