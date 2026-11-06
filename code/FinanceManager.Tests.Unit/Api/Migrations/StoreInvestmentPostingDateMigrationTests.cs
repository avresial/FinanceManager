using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FinanceManager.Tests.Unit.Api.Migrations;

public class StoreInvestmentPostingDateMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Scripts_ConvertDatesInBothDirections(bool sqlServer)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        if (sqlServer)
            options.UseSqlServer("Server=localhost;Database=script-only;Trusted_Connection=True", b => b.MigrationsAssembly("FinanceManager.Api"));
        else
            options.UseNpgsql("Host=localhost;Database=script-only", b => b.MigrationsAssembly("FinanceManager.Api"));
        using var context = new AppDbContext(options.Options);
        var migrator = context.GetService<IMigrator>();
        var up = migrator.GenerateScript("WidenEntryIdsToLong", "StoreInvestmentPostingDate");
        var down = migrator.GenerateScript("StoreInvestmentPostingDate", "WidenEntryIdsToLong");
        Assert.DoesNotContain("DROP COLUMN", up);
        Assert.DoesNotContain("DROP COLUMN", down);
        Assert.Contains(sqlServer ? "ALTER COLUMN [TradeDate] datetime2 NOT NULL" : """("TradeDate"::timestamp AT TIME ZONE 'UTC')""", up);
        Assert.Contains(sqlServer ? "ALTER COLUMN [PostingDate] date NOT NULL" : """("PostingDate" AT TIME ZONE 'UTC')::date""", down);
        foreach (var script in new[] { up, down })
        {
            Assert.Contains("DROP INDEX", script);
            Assert.Contains("CREATE INDEX", script);
        }
        if (Environment.GetEnvironmentVariable("FM_MIGRATION_SCRIPTS") is string directory)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, sqlServer ? "sql-up.sql" : "pg-up.sql"), up);
            File.WriteAllText(Path.Combine(directory, sqlServer ? "sql-down.sql" : "pg-down.sql"), down);
            if (!sqlServer)
                File.WriteAllText(Path.Combine(directory, "pg-baseline.sql"), migrator.GenerateScript("0", "WidenEntryIdsToLong"));
        }
    }
}