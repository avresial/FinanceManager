using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Investments.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

/// <summary>
/// Covers behaviour SQLite can't show: row locks between concurrent writers, and decimal columns that round to scale 8.
/// Set FM_VALUE_POSTGRESQL_CONNECTION to a disposable database to run these.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PostgresInvestmentRunningValueTests
{
    private const int _accountId = 1;
    private const long _listingId = 1;
    private static readonly DateOnly _day = new(2026, 3, 1);
    private static readonly string? _connection = Environment.GetEnvironmentVariable("FM_VALUE_POSTGRESQL_CONNECTION");

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ConcurrentAdds_ToOneAccount_StoreCorrectRunningSum()
    {
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(_connection), "Set FM_VALUE_POSTGRESQL_CONNECTION to run this test.");
        await ResetDatabase();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var context = CreateContext();
            await new InvestmentTransactionRepository(context).Add(Trade(1m), Ct);
        }));

        await using var verify = CreateContext();
        var values = await verify.InvestmentTransactions.AsNoTracking()
            .OrderBy(x => x.PostingDate).ThenBy(x => x.EntryId).Select(x => x.Value).ToListAsync(Ct);
        Assert.Equal(Enumerable.Range(1, 20).Select(x => (decimal)x), values);
    }

    [Fact]
    public async Task Add_WithExcessPrecision_CalculatesFromStoredQuantities()
    {
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(_connection), "Set FM_VALUE_POSTGRESQL_CONNECTION to run this test.");
        await ResetDatabase();

        await using var context = CreateContext();
        var repository = new InvestmentTransactionRepository(context);
        await repository.Add(Trade(0.000000006m), Ct);
        await repository.Add(Trade(0.000000006m), Ct);

        await using var verify = CreateContext();
        var stored = await verify.InvestmentTransactions.AsNoTracking()
            .OrderBy(x => x.EntryId).Select(x => new { x.Quantity, x.Value }).ToListAsync(Ct);
        Assert.Equal([0.00000001m, 0.00000001m], stored.Select(x => x.Quantity));
        Assert.Equal([0.00000001m, 0.00000002m], stored.Select(x => x.Value));
    }

    private async Task ResetDatabase()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync(Ct);
        await context.Database.EnsureCreatedAsync(Ct);
        context.Accounts.Add(new FinancialAccountBaseDto { AccountId = _accountId, UserId = 1, Name = "Investments", AccountLabel = AccountLabel.Other, AccountType = AccountType.Stock });
        context.Set<AssetListing>().Add(new AssetListing
        {
            Id = _listingId,
            Asset = new Asset { Id = 1, Name = "Test", Type = AssetType.ETF },
            Ticker = "TEST",
            ExchangeMic = "XNAS",
            ExchangeName = "Test",
            TradingCurrency = "USD",
            PriceMultiplier = 1
        });
        await context.SaveChangesAsync(Ct);
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connection).Options);

    private static InvestmentTransaction Trade(decimal quantity) => new()
    {
        AccountId = _accountId,
        UserId = 1,
        AssetListingId = _listingId,
        Currency = "USD",
        Quantity = quantity,
        UnitPrice = 10,
        Type = InvestmentTransactionType.Buy,
        TradeDate = _day
    };
}