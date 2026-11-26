using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FinanceManager.Tests.Unit.Api.Migrations;

[Trait("Category", "Unit")]
public class EnforceTransactionOwnerKeysMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CatchUpAndPreconditionsPrecedeRequiredColumns(bool sqlServer)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        if (sqlServer) options.UseSqlServer("Server=localhost;Database=script-only", b => b.MigrationsAssembly("FinanceManager.Api"));
        else options.UseNpgsql("Host=localhost;Database=script-only", b => b.MigrationsAssembly("FinanceManager.Api"));
        using var context = new AppDbContext(options.Options);
        var migrator = context.GetService<IMigrator>();
        string Script(string from, string to) => migrator.GenerateScript(from, to).Replace("\"", "").Replace("[", "").Replace("]", "");
        const string previous = "MigrateAlertsToCurrencyScope", migration = "EnforceTransactionOwnerKeys";
        var up = Script(previous, migration);
        var down = Script(migration, previous);
        var abort = up.IndexOf(sqlServer ? "THROW 50000" : "RAISE EXCEPTION", StringComparison.Ordinal);
        Assert.InRange(up.IndexOf("UPDATE", StringComparison.Ordinal), 0, abort - 1);
        Assert.True(abort < up.IndexOf("ALTER TABLE", StringComparison.Ordinal));
        Assert.Contains("1, Id, AccountId, CAST(UserId AS bigint), NULL FROM InvestmentTransactions", up);
        foreach (var category in new[] { "null_or_zero_keys", "invalid_cash_currency", "account_mismatch", "legacy_alert_scope" })
            Assert.Contains(category + "=", up);
        Assert.DoesNotContain("DEFAULT 0", up);
        Assert.DoesNotContain("SET UserId = 0", up);
        Assert.DoesNotContain("SET CurrencyId = 0", up);
        foreach (var (table, column) in new[] { ("CurrencyEntries", "UserId"), ("CurrencyEntries", "CurrencyId"), ("BondEntries", "UserId") })
        {
            Assert.Contains($"ALTER TABLE {table} ALTER COLUMN {column} " + (sqlServer ? "int NOT NULL" : "SET NOT NULL"), up);
            Assert.Contains($"ALTER TABLE {table} ALTER COLUMN {column} " + (sqlServer ? "int NULL" : "DROP NOT NULL"), down);
        }
        foreach (var script in new[] { up, down })
            foreach (var index in new[] { "IX_CurrencyEntries_OwnerCurrencyDateId", "IX_CurrencyEntries_OwnerDateId", "IX_BondEntries_OwnerBondDateId", "IX_BondEntries_OwnerDateId" })
            {
                var drop = script.IndexOf($"DROP INDEX {index} ", StringComparison.Ordinal);
                var create = script.IndexOf($"CREATE INDEX {index} ", StringComparison.Ordinal);
                var alter = script.IndexOf("ALTER TABLE", StringComparison.Ordinal);
                if (sqlServer) Assert.True(drop >= 0 && drop < alter && alter < create);
                else Assert.True(drop == -1 && create == -1);
            }
    }
}