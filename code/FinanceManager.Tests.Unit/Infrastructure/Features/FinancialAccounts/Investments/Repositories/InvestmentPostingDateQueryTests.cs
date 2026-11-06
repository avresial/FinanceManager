using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Infrastructure.Features.Assets.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Investments.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.FinancialAccounts.Investments.Repositories;

public class InvestmentPostingDateQueryTests
{
    [Fact]
    public async Task AllDateQueries_ExecuteOnPostgreSql()
    {
        var connection = Environment.GetEnvironmentVariable("FM_QUERY_POSTGRES");
        Assert.SkipWhen(connection is null, "Set FM_QUERY_POSTGRES to a disposable migrated PostgreSQL database.");
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        var repository = new InvestmentTransactionRepository(context);
        var ct = TestContext.Current.CancellationToken;
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 12, 31);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var listing = new AssetListing
        {
            Id = 1081,
            Asset = new Asset { Id = 1081, Name = "PostingDate test", Type = AssetType.ETF },
            Ticker = "DATE",
            ExchangeMic = "XNAS",
            ExchangeName = "Test",
            TradingCurrency = "USD",
            PriceMultiplier = 1
        };
        context.InvestmentTransactions.AddRange(new[] { start.AddDays(-1), start, end.AddDays(1) }.Select((date, index) =>
            new InvestmentTransaction
            {
                EntryId = 1081 + index,
                AccountId = 1081,
                UserId = 1081,
                AssetListing = listing,
                Currency = "USD",
                Quantity = 1,
                UnitPrice = 10,
                TradeDate = date
            }));
        await context.SaveChangesAsync(ct);
        context.ChangeTracker.Clear();
        Assert.Equal(3, (await repository.GetByAccount(1081, ct)).Count);
        await repository.GetByAccountAndIds(1081, [1081], ct);
        await repository.GetMostRecentByAccounts([1081], 10, ct);
        await repository.GetByUser(1081, start, end, ct);
        await repository.GetHoldingsAsOf([1081], end, ct);
        await repository.GetHoldingsByAccountAsOf([1081], end, ct);
        await repository.GetValuationInputs([1081], start, end, ct);
        var flows = await repository.GetCapitalFlowInputs([1081], end, ct);
        Assert.Equal(new[] { start.AddDays(-1), start }, flows.Select(flow => flow.TradeDate));
        var holdings = await repository.GetHoldingsAsOf([1081], end, ct);
        Assert.Equal(2m, holdings[1081]);
        await repository.GetLatestByListingAsOf(1081, end, ct);
        await repository.GetHistoryPage([1081], start, end, 10, end, 100, ct);
        await repository.GetHistoryPage(1081, 10, end, 100, start, end, cancellationToken: ct);
        await new AssetRepository(context).GetIncomplete(1, 10, null, DateTimeOffset.UtcNow, ct);
    }
}