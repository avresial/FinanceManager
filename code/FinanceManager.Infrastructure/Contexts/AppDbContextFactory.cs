using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FinanceManager.Infrastructure.Contexts;

/// <summary>
/// Design-time factory for EF Core migrations.
/// This is used by EF Core tools when creating, updating, or removing migrations.
/// It ensures a relational database provider is configured (PostgreSQL by default for migrations).
/// 
/// For production use, set the environment variable or pass the connection string via:
/// dotnet ef database update --connection "Host=...;Database=..."
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var apiDirectory = ResolveApiDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, b => b.MigrationsAssembly("FinanceManager.Api"));

        return new AppDbContext(optionsBuilder.Options);
    }

    private static string ResolveApiDirectory()
    {
        var current = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            current,
            Path.Combine(current, "code"),
            Path.GetFullPath(Path.Combine(current, "..")),
            Path.GetFullPath(Path.Combine(current, "..", "code"))
        };

        foreach (var candidate in candidates)
        {
            var apiPath = Path.Combine(candidate, "FinanceManager.Api");
            if (Directory.Exists(apiPath))
            {
                return apiPath;
            }
        }

        throw new DirectoryNotFoundException("Unable to locate FinanceManager.Api directory for configuration loading.");
    }
}