using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FinanceManager.Tests.Unit.Api.Migrations;

public class ExpandTransactionOwnerKeysMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExpandAndRollbackPreserveLegacyRelationships(bool sqlServer)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        if (sqlServer) options.UseSqlServer("Server=localhost;Database=script-only", b => b.MigrationsAssembly("FinanceManager.Api"));
        else options.UseNpgsql("Host=localhost;Database=script-only", b => b.MigrationsAssembly("FinanceManager.Api"));
        using var context = new AppDbContext(options.Options);
        var migrator = context.GetService<IMigrator>();
        string Script(string from, string to) => migrator.GenerateScript(from, to).Replace("\"", "").Replace("[", "").Replace("]", "");
        const string previous = "StoreInvestmentRunningUnits", migration = "ExpandTransactionOwnerKeys";
        var up = Script(previous, migration);
        var down = Script(migration, previous);
        foreach (var table in new[] { "CurrencyEntries", "BondEntries", "InvestmentTransactions", "DividendPayments" })
        {
            Assert.Contains($"ALTER TABLE {table} ALTER COLUMN AccountId " + (sqlServer ? "int NULL;" : "DROP NOT NULL;"), up);
            Assert.Contains($"FROM {table} WHERE AccountId IS NULL", down);
            Assert.Contains($"ALTER TABLE {table} ALTER COLUMN AccountId " + (sqlServer ? "int NOT NULL;" : "SET NOT NULL;"), down);
        }
        foreach (var (table, column) in new[] { ("CurrencyEntries", "CurrencyId"), ("CurrencyEntries", "UserId"), ("BondEntries", "UserId") })
            Assert.Contains($"ALTER TABLE {table} ADD {column} " + (sqlServer ? "int NULL;" : "integer;"), up);

        Assert.Contains("CurrencyId = a.CurrencyId", up);
        Assert.Contains("a.AccountType = 0 AND a.UserId <> 0", up);
        Assert.Contains("a.AccountType = 2 AND a.UserId <> 0", up);
        Assert.DoesNotContain("a.CurrencyId <> 0", up);
        Assert.DoesNotContain("ON DELETE CASCADE", up);
        Assert.Contains("FOREIGN KEY (AccountId) REFERENCES Accounts (AccountId)", up);
        Assert.Contains("FOREIGN KEY (AccountId) REFERENCES Accounts (AccountId) ON DELETE CASCADE", down);
        Assert.DoesNotContain("FinancialLabel", up + down);
        Assert.True(down.IndexOf("Cannot roll back", StringComparison.Ordinal) < down.IndexOf("DROP CONSTRAINT", StringComparison.Ordinal));

        foreach (var (table, suffix, keys) in new[] {
            ("CurrencyEntries", "OwnerCurrencyDateId", "UserId, CurrencyId, PostingDate, EntryId"),
            ("BondEntries", "OwnerBondDateId", "UserId, BondDetailsId, PostingDate, EntryId"),
            ("InvestmentTransactions", "OwnerListingDateId", "UserId, AssetListingId, PostingDate, Id"),
            ("DividendPayments", "OwnerPaymentDateId", "UserId, PaymentDate, Id") })
        {
            Assert.Contains($"CREATE INDEX IX_{table}_{suffix} ON {table} ({keys})", up);
            Assert.Contains($"DROP INDEX IX_{table}_{suffix}", down);
        }
        if (sqlServer)
        {
            foreach (var script in new[] { up, down })
            {
                Assert.Contains("DROP INDEX IX_DividendPayments_AccountId ON DividendPayments", script);
                Assert.Contains("CREATE INDEX IX_DividendPayments_AccountId ON DividendPayments (AccountId)", script);
                Assert.Contains("CREATE INDEX IX_InvestmentTransactions_AccountId_PostingDate_Id ON InvestmentTransactions (AccountId, PostingDate, Id)", script);
            }
        }
    }
}