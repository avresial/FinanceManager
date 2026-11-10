using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Text.RegularExpressions;

namespace FinanceManager.Tests.Unit.Api.Migrations;

public class WidenEntryIdsMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MigrationScript_WidensEntryKeysAndLabelForeignKeysForBothProviders(bool sqlServer)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        if (sqlServer)
            options.UseSqlServer("Server=localhost;Database=script-only;Trusted_Connection=True", b => b.MigrationsAssembly("FinanceManager.Api"));
        else
            options.UseNpgsql("Host=localhost;Database=script-only", b => b.MigrationsAssembly("FinanceManager.Api"));
        using var context = new AppDbContext(options.Options);
        var migrator = context.GetService<IMigrator>();

        var up = migrator.GenerateScript("AddDividendPayments", "WidenEntryIdsToLong");

        foreach (var (table, column) in new[]
        {
            ("CurrencyEntries", "EntryId"),
            ("BondEntries", "EntryId"),
            ("CurrencyAccountEntryFinancialLabel", "CurrencyAccountEntryEntryId"),
            ("FinancialLabels", "BondAccountEntryEntryId"),
        })
            Assert.Matches($@"ALTER TABLE {Name(table)} ALTER COLUMN {Name(column)} (TYPE )?bigint", up);

        foreach (var (table, key) in new[]
        {
            ("CurrencyEntries", "PK_CurrencyEntries"),
            ("BondEntries", "PK_BondEntries"),
            ("CurrencyAccountEntryFinancialLabel", "PK_CurrencyAccountEntryFinancialLabel"),
        })
            Assert.Matches($@"ALTER TABLE {Name(table)} ADD CONSTRAINT {Name(key)} PRIMARY KEY", up);

        foreach (var (table, foreignKey) in new[]
        {
            ("CurrencyAccountEntryFinancialLabel", "FK_CurrencyAccountEntryFinancialLabel_CurrencyEntries_Currency~"),
            ("FinancialLabels", "FK_FinancialLabels_BondEntries_BondAccountEntryEntryId"),
        })
            Assert.Matches($@"ALTER TABLE {Name(table)} ADD CONSTRAINT {Name(foreignKey)} FOREIGN KEY", up);

        foreach (var index in new[]
        {
            "IX_CurrencyEntries_AccountId_PostingDate_EntryId",
            "IX_BondEntries_AccountId_PostingDate_EntryId",
            "IX_BondEntries_AccountId_BondDetailsId_PostingDate_EntryId",
            "IX_FinancialLabels_BondAccountEntryEntryId",
        })
            Assert.Matches($@"CREATE INDEX {Name(index)}", up);
    }

    // Quoting differs by provider: "Name" on PostgreSQL and [Name] on SQL Server.
    private static string Name(string identifier) => $@"[""\[]{Regex.Escape(identifier)}[""\]]";
}