using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FinanceManager.Tests.Unit.Api.Migrations;

public class InvestmentTransactionHistoryIndexMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MigrationScript_CreatesAndDropsPagingIndexForBothProviders(bool sqlServer)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        if (sqlServer)
            options.UseSqlServer("Server=localhost;Database=script-only;Trusted_Connection=True", b => b.MigrationsAssembly("FinanceManager.Api"));
        else
            options.UseNpgsql("Host=localhost;Database=script-only", b => b.MigrationsAssembly("FinanceManager.Api"));
        using var context = new AppDbContext(options.Options);
        var migrator = context.GetService<IMigrator>();

        var up = migrator.GenerateScript("AddLifetimeGiftCodes", "AddInvestmentTransactionHistoryIndex");
        var down = migrator.GenerateScript("AddInvestmentTransactionHistoryIndex", "AddLifetimeGiftCodes");

        Assert.Contains("CREATE INDEX", up, StringComparison.Ordinal);
        Assert.Contains("IX_InvestmentTransactions_AccountId_TradeDate_Id", up, StringComparison.Ordinal);
        Assert.Contains(sqlServer ? "([AccountId], [TradeDate], [Id])" : "(\"AccountId\", \"TradeDate\", \"Id\")", up, StringComparison.Ordinal);
        Assert.Contains("DROP INDEX", down, StringComparison.Ordinal);
        Assert.Contains("IX_InvestmentTransactions_AccountId_TradeDate_Id", down, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER TABLE", up, StringComparison.Ordinal);
    }
}