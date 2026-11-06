using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Text.RegularExpressions;

namespace FinanceManager.Tests.Unit.Api.Migrations;

public class StoreInvestmentRunningUnitsMigrationTests
{
    private const string _previous = "AddInvestmentTransactionLabels";
    private const string _migration = "StoreInvestmentRunningUnits";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MigrationScript_AddsValueColumnAndBackfillsRunningUnitsForBothProviders(bool sqlServer)
    {
        var up = Script(sqlServer, _previous, _migration);

        Assert.Matches($@"ALTER TABLE {Name("InvestmentTransactions")} ADD {Name("Value")} numeric\(18,8\) NOT NULL DEFAULT 0", up);

        // Buy (0) adds and Sell (1) subtracts the quantity, summed per account and listing in date then id order.
        Assert.Matches($@"SUM\(CASE {Name("Type")} WHEN 0 THEN {Name("Quantity")} WHEN 1 THEN -{Name("Quantity")} ELSE 0 END\) OVER \(\s*PARTITION BY {Name("AccountId")}, {Name("AssetListingId")}\s*ORDER BY {Name("PostingDate")}, {Name("Id")}\s*ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW\)", up);

        // The backfill must run after the column exists.
        Assert.True(up.IndexOf("ADD ", StringComparison.Ordinal) < up.IndexOf("UPDATE ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MigrationScript_DownDropsValueColumnForBothProviders(bool sqlServer)
    {
        var down = Script(sqlServer, _migration, _previous);

        Assert.Matches($@"ALTER TABLE {Name("InvestmentTransactions")} DROP COLUMN {Name("Value")}", down);
    }

    private static string Script(bool sqlServer, string from, string to)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        if (sqlServer)
            options.UseSqlServer("Server=localhost;Database=script-only;Trusted_Connection=True", b => b.MigrationsAssembly("FinanceManager.Api"));
        else
            options.UseNpgsql("Host=localhost;Database=script-only", b => b.MigrationsAssembly("FinanceManager.Api"));
        using var context = new AppDbContext(options.Options);
        return context.GetService<IMigrator>().GenerateScript(from, to);
    }

    // Quoting differs by provider: "Name" on PostgreSQL and [Name] on SQL Server.
    private static string Name(string identifier) => $@"[""\[]{Regex.Escape(identifier)}[""\]]";
}