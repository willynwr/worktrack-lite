namespace WorkTrack.Api.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Design-time factory untuk `dotnet ef migrations` tanpa butuh koneksi MySQL aktif.
/// Connection string disini hanya dipakai saat generate migration, bukan saat runtime.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Dummy connection string — hanya untuk generate migration (tidak perlu server aktif)
        optionsBuilder.UseMySql(
            "server=localhost;database=worktrack;user=root;password=",
            new MySqlServerVersion(new Version(8, 0, 0))
        );

        return new AppDbContext(optionsBuilder.Options);
    }
}
