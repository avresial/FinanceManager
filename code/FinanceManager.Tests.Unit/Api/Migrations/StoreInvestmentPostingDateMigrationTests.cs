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
        Assert.Matches(sqlServer ? @"sp_rename.*TradeDate.*PostingDate" : """RENAME COLUMN "TradeDate" TO "PostingDate" """.TrimEnd(), up);
        Assert.Matches(sqlServer ? @"sp_rename.*PostingDate.*TradeDate" : """RENAME COLUMN "PostingDate" TO "TradeDate" """.TrimEnd(), down);
        foreach (var index in new[] { "IX_InvestmentTransactions_AccountId_PostingDate_Id", "IX_InvestmentTransactions_UserId_PostingDate" })
            Assert.Matches($@"CREATE INDEX [""\[]{index}[""\]]", up);
        foreach (var index in new[] { "IX_InvestmentTransactions_AccountId_TradeDate_Id", "IX_InvestmentTransactions_UserId_TradeDate" })
        {
            Assert.Matches($@"DROP INDEX .*{index}", up);
            Assert.Matches($@"CREATE INDEX [""\[]{index}[""\]]", down);
        }
    }
}