using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

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

        Assert.Contains("PK_CurrencyEntries", up, StringComparison.Ordinal);
        Assert.Contains("PK_BondEntries", up, StringComparison.Ordinal);
        foreach (var column in new[] { "EntryId", "CurrencyAccountEntryEntryId", "BondAccountEntryEntryId" })
            Assert.Contains(column, up, StringComparison.Ordinal);
        Assert.Contains("bigint", up, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FK_FinancialLabels_BondEntries_BondAccountEntryEntryId", up, StringComparison.Ordinal);
    }
}