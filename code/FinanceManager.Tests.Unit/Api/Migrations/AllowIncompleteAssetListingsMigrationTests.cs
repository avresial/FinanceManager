using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FinanceManager.Tests.Unit.Api.Migrations;

[Trait("Category", "Unit")]
public class AllowIncompleteAssetListingsMigrationTests
{
    [Theory]
    [InlineData(
        false,
        "ALTER COLUMN \"ExchangeMic\" DROP NOT NULL",
        "ALTER COLUMN \"TradingCurrency\" DROP NOT NULL",
        "\"ExchangeMic\" IS NOT NULL AND \"TradingCurrency\" IS NOT NULL")]
    [InlineData(
        true,
        "ALTER COLUMN [ExchangeMic] nvarchar(16) NULL",
        "ALTER COLUMN [TradingCurrency] nvarchar(3) NULL",
        "[ExchangeMic] IS NOT NULL AND [TradingCurrency] IS NOT NULL")]
    public void MigrationScript_AltersColumnsAndFilteredUniqueIndexForBothProviders(
        bool sqlServer,
        string expectedMicAlter,
        string expectedCurrencyAlter,
        string expectedFilter)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        if (sqlServer)
            options.UseSqlServer("Server=localhost;Database=script-only;Trusted_Connection=True", b => b.MigrationsAssembly("FinanceManager.Api"));
        else
            options.UseNpgsql("Host=localhost;Database=script-only", b => b.MigrationsAssembly("FinanceManager.Api"));

        using var context = new AppDbContext(options.Options);
        var migrator = context.GetService<IMigrator>();

        var up = migrator.GenerateScript("AddInvestmentTransactionHistoryIndex", "AllowIncompleteAssetListings");
        var down = migrator.GenerateScript("AllowIncompleteAssetListings", "AddInvestmentTransactionHistoryIndex");

        Assert.Contains("IX_AssetListings_Ticker_ExchangeMic_TradingCurrency", up, StringComparison.Ordinal);
        Assert.Contains(expectedMicAlter, up, StringComparison.Ordinal);
        Assert.Contains(expectedCurrencyAlter, up, StringComparison.Ordinal);
        Assert.Contains(expectedFilter, up, StringComparison.Ordinal);
        Assert.DoesNotContain(sqlServer ? "character varying" : "nvarchar", up, StringComparison.Ordinal);

        Assert.Contains("IX_AssetListings_Ticker_ExchangeMic_TradingCurrency", down, StringComparison.Ordinal);
        Assert.Contains("WHERE", down, StringComparison.Ordinal);
        Assert.DoesNotContain(expectedFilter, down, StringComparison.Ordinal);
    }
}