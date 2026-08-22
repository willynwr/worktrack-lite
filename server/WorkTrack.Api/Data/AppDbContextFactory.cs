namespace WorkTrack.Api.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Design-time factory untuk `dotnet ef migrations`/`dotnet ef database update` — dipakai CLI
/// tooling, bukan Program.cs, jadi tidak otomatis baca appsettings.json.
/// Connection string diambil dari environment variable ConnectionStrings__DefaultConnection
/// bila di-set (mis. saat `dotnet ef database update` di server), fallback ke dummy lokal
/// (tidak perlu server MySQL aktif — cukup untuk `dotnet ef migrations add`).
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "server=localhost;database=worktrack;user=root;password=";

        optionsBuilder.UseMySql(
            connectionString,
            new MySqlServerVersion(new Version(8, 0, 0))
        );

        return new AppDbContext(optionsBuilder.Options);
    }
}
