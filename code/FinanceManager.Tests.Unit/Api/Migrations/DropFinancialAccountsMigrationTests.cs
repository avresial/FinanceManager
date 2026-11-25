using FinanceManager.Api.Migrations;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Text.RegularExpressions;

namespace FinanceManager.Tests.Unit.Api.Migrations;

[Trait("Category", "Unit")]
public class DropFinancialAccountsMigrationTests
{
    private const string _previous = "EnforceTransactionOwnerKeys", _migration = "DropFinancialAccounts";

    private static readonly string[] _expectedDrops =
    [
        "ALTER TABLE DividendPayments DROP CONSTRAINT FK_DividendPayments_Accounts_AccountId",
        "DROP INDEX IX_CurrencyEntries_AccountId_PostingDate_EntryId",
        "DROP INDEX IX_BondEntries_AccountId_PostingDate_EntryId",
        "DROP INDEX IX_BondEntries_AccountId_BondDetailsId_PostingDate_EntryId",
        "DROP INDEX IX_InvestmentTransactions_AccountId",
        "DROP INDEX IX_InvestmentTransactions_AccountId_PostingDate_Id",
        "DROP INDEX IX_DividendPayments_AccountId",
        "DROP INDEX IX_DividendPayments_UserId_AccountId_PaymentDate_Id",
        "DROP INDEX IX_Accounts_AccountType_UserId",
        "ALTER TABLE CurrencyEntries DROP COLUMN AccountId",
        "ALTER TABLE BondEntries DROP COLUMN AccountId",
        "ALTER TABLE InvestmentTransactions DROP COLUMN AccountId",
        "ALTER TABLE DividendPayments DROP COLUMN AccountId",
        "ALTER TABLE FinancialAlerts DROP COLUMN AccountId",
        "ALTER TABLE FinancialInsights DROP COLUMN AccountId",
        "ALTER TABLE CurrencyEntries DROP COLUMN Value",
        "ALTER TABLE BondEntries DROP COLUMN Value",
        "ALTER TABLE InvestmentTransactions DROP COLUMN Value",
        "DROP TABLE Accounts",
    ];

    private static IMigrator Migrator(AppDbContext context) => context.GetService<IMigrator>();

    private static AppDbContext Context(bool sqlServer)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        if (sqlServer) options.UseSqlServer("Server=localhost;Database=script-only", b => b.MigrationsAssembly("FinanceManager.Api"));
        else options.UseNpgsql("Host=localhost;Database=script-only", b => b.MigrationsAssembly("FinanceManager.Api"));
        return new AppDbContext(options.Options);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Up_DropsExactlyTheAccountSchema_InDependencyOrder(bool sqlServer)
    {
        using var context = Context(sqlServer);
        var up = Migrator(context).GenerateScript(_previous, _migration).Replace("\"", "").Replace("[", "").Replace("]", "");

        // Every DROP statement in the script, in order, so nothing else is dropped. SQL Server's "ON <table>" after
        // DROP INDEX and its dynamic default-constraint drop (no literal name) are not matched.
        var drops = Regex.Matches(up, @"(ALTER TABLE \w+ DROP (?:CONSTRAINT|COLUMN) \w+|DROP (?:INDEX|TABLE) \w+)").Select(match => match.Value);
        Assert.Equal(_expectedDrops, drops);

        // SQL Server: EF drops a column's default constraint first if it has one (InvestmentTransactions.Value does).
        if (sqlServer) Assert.Equal(9, Regex.Matches(up, @"IF @var\d* IS NOT NULL EXEC\(N'ALTER TABLE \w+ DROP CONSTRAINT ' \+ @var\d* \+ ';'\);").Count);
        else Assert.DoesNotContain("default_constraints", up);

        // Owner indexes, the unrelated listing index, label join tables and rows are untouched.
        Assert.DoesNotContain("_Owner", up);
        Assert.DoesNotContain("IX_InvestmentTransactions_UserId_AssetListingId", up);
        Assert.DoesNotContain("FinancialLabel", up);
        Assert.DoesNotContain("DELETE", up);
        Assert.DoesNotContain("UPDATE", up);
        Assert.DoesNotContain("CREATE", up);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Down_RefusesAndPointsToTheBackup(bool sqlServer)
    {
        using var context = Context(sqlServer);

        var error = Assert.Throws<NotSupportedException>(() => Migrator(context).GenerateScript(_migration, _previous));
        Assert.Equal("Restore from the pre-contract backup.", error.Message);
        Assert.Throws<NotSupportedException>(() => new DropFinancialAccounts { ActiveProvider = context.Database.ProviderName! }.DownOperations);
    }
}