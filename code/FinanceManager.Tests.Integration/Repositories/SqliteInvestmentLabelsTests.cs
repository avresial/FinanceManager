using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Investments.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

public class SqliteInvestmentLabelsTests
{
    [Fact]
    public async Task PagingCountsTradesAndBothDeletesCascade()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(ct);
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync(ct);
        var asset = new Asset { Name = "Test", Type = AssetType.ETF };
        var listing = new AssetListing { Asset = asset, Ticker = "TEST", ExchangeName = "Test", TradingCurrency = "USD" };
        var labels = Enumerable.Range(1, 4).Select(i => new FinancialLabel { Name = $"Label {i}" }).ToArray();
        context.AssetListings.Add(listing);
        var trades = Enumerable.Range(1, 5).Select(i => new InvestmentTransaction
        {
            AccountId = 1,
            UserId = 1,
            AssetListing = listing,
            Quantity = 1,
            UnitPrice = 10,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 1, i),
            Labels = labels.ToList()
        }).ToArray();
        context.InvestmentTransactions.AddRange(trades);
        await context.SaveChangesAsync(ct);
        context.ChangeTracker.Clear();
        var repository = new InvestmentTransactionRepository(context);
        var (page, more) = await repository.GetHistoryPage(1, 2, cancellationToken: ct);
        Assert.True(more);
        Assert.Equal(2, page.Count);
        Assert.Equal(2, page.Select(x => x.EntryId).Distinct().Count());
        Assert.All(page, x => Assert.Equal(4, x.Labels.Count));
        var (next, _) = await repository.GetHistoryPage(1, 2, page[^1].TradeDate, page[^1].EntryId, cancellationToken: ct);
        Assert.Equal(2, next.Count);
        Assert.Empty(page.Select(x => x.EntryId).Intersect(next.Select(x => x.EntryId)));
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM InvestmentTransactions WHERE Id = {trades[0].EntryId}", ct);
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM FinancialLabels WHERE Id = {labels[0].Id}", ct);
        context.ChangeTracker.Clear();
        var remaining = await repository.GetByAccount(1, ct);
        Assert.Equal(4, remaining.Count);
        Assert.All(remaining, x => Assert.Equal(3, x.Labels.Count));
        Assert.Equal(12, await context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM InvestmentTransactionFinancialLabel").SingleAsync(ct));
    }
}