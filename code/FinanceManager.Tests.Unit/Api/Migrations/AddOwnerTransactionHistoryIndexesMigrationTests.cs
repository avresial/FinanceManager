using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FinanceManager.Tests.Unit.Api.Migrations;

public class AddOwnerTransactionHistoryIndexesMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreatesOwnerDateIndexesAndCleansOnlyLinkedOrphans(bool sqlServer)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        if (sqlServer) options.UseSqlServer("Server=localhost;Database=script-only", b => b.MigrationsAssembly("FinanceManager.Api"));
        else options.UseNpgsql("Host=localhost;Database=script-only", b => b.MigrationsAssembly("FinanceManager.Api"));
        using var context = new AppDbContext(options.Options);
        var migrator = context.GetService<IMigrator>();
        string Script(string from, string to) => migrator.GenerateScript(from, to).Replace("\"", "").Replace("[", "").Replace("]", "");
        const string previous = "ExpandTransactionOwnerKeys", migration = "AddOwnerTransactionHistoryIndexes";
        var up = Script(previous, migration);
        var down = Script(migration, previous);
        foreach (var table in new[] { "CurrencyEntries", "BondEntries" })
        {
            Assert.Contains($"CREATE INDEX IX_{table}_OwnerDateId ON {table} (UserId, PostingDate, EntryId)", up);
            Assert.Contains($"DROP INDEX IX_{table}_OwnerDateId", down);
        }
        foreach (var table in new[] { "InvestmentTransactions", "DividendPayments" })
        {
            Assert.Contains((sqlServer ? "DELETE e FROM " : "DELETE FROM ") + table + " AS e", up);
        }
        Assert.Contains("WHERE e.AccountId IS NOT NULL AND NOT EXISTS", up);
        Assert.Contains("a.AccountId = e.AccountId AND a.AccountType = 1", up);
        Assert.DoesNotContain("InvestmentTransactions", down);
        Assert.DoesNotContain("DividendPayments", down);
    }
}