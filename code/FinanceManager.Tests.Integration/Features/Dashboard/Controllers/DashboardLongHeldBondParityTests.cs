using FinanceManager.Application.MoneyFlow.NetWorth;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Contracts.Transactions.Bonds;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Shared;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Dashboard.Controllers;

[Trait("Category", "Integration")]
public class DashboardLongHeldBondParityTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const int _userId = 1028;
    private static readonly DateTime _end = new(2024, 6, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _start = _end.AddMonths(-6);
    private TestDatabase? _database;
    private bool _pointPricing;
    private int _rangeCalls;
    private IServiceScopeFactory? _scopeFactory;

    protected override bool ReuseApplicationHost => false;

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<DbContextOptions<AppDbContext>>();
        _database = new TestDatabase();
        services.AddSingleton(_database.Context);
        services.AddScoped<INetWorthService>(provider =>
        {
            _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            return new ReferenceNetWorthService(ActivatorUtilities.CreateInstance<NetWorthService>(provider), this);
        });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public async Task Overview_LongHeldEntry_CompleteResponseMatchesPointPricing_AndRefreshesAfterEdits(int months)
    {
        var ct = TestContext.Current.CancellationToken;
        var context = _database!.Context;
        var currency = new Currency(0, "PLN", "zł");
        context.Currencies.Add(currency);
        var posting = _start.Date.AddDays(-3650);
        var details = new BondDetails("Long-held", "Synthetic", DateOnly.FromDateTime(posting), new(2025, 1, 1),
            [new() { DateOperator = DateOperator.UntilDate, DateValue = "2025-01-01", Rate = 0.05m }], currency)
        { Id = 1028 };
        context.Bonds.Add(details);
        var entry = new BondEntry(1028, posting, 1m, details.Id) { UserId = _userId };
        context.BondEntries.Add(entry);
        await context.SaveChangesAsync(ct);
        Authorize("testuser", _userId, UserRole.User);
        var start = _end.AddMonths(-months);

        async Task<JsonNode> Overview()
        {
            using var response = await Client.GetAsync($"api/Dashboard/overview?userId={_userId}&currencyId=0&start={start:O}&end={_end:O}", ct);
            response.EnsureSuccessStatusCode();
            return JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))!;
        }

        // Real repositories and the complete dashboard composition; only the net-worth range
        // calculation switches to the pre-optimization daily point-pricing oracle.
        _pointPricing = true;
        var baseline = await Overview();
        await Invalidate();
        _pointPricing = false;
        var candidate = await Overview();
        Assert.True(JsonNode.DeepEquals(baseline, candidate));
        var calls = _rangeCalls;
        Assert.True(JsonNode.DeepEquals(candidate, await Overview()));
        Assert.Equal(calls, _rangeCalls); // Warm aggregate hits still skip net-worth calculation.

        using (var scope = _scopeFactory!.CreateScope())
        {
            var loadedDetails = Assert.Single(await scope.ServiceProvider.GetRequiredService<IBondDetailsRepository>().GetByIdsAsync([1028], ct));
            var series = candidate["netWorthSeries"]!.AsArray();
            Assert.Equal((int)(_end - start).TotalDays + 1, series.Count);
            foreach (var point in series)
            {
                var date = point!["dateTime"]!.GetValue<DateTime>();
                Assert.True((start.Date - entry.PostingDate.Date).Days >= 3650);
                Assert.Equal(Math.Round(BondEntry.GetPrice(DateOnly.FromDateTime(entry.PostingDate), entry.ValueChange, DateOnly.FromDateTime(date), loadedDetails)[DateOnly.FromDateTime(date)], 2), point["value"]!.GetValue<decimal>());
            }
        }

        using var update = await Client.PutAsJsonAsync($"api/transactions/bonds/{entry.EntryId}",
            new SaveBondEntryRequest(details.Id, posting, 2m, entry.Description), ct);
        update.EnsureSuccessStatusCode();
        Assert.Equal(2m, (await update.Content.ReadFromJsonAsync<BondTransactionLogItemDto>(ct))!.ValueChange);
        var changedEntry = await Overview(); // The PUT must invalidate the cached overview itself.
        Assert.False(JsonNode.DeepEquals(candidate["netWorthSeries"], changedEntry["netWorthSeries"]));
        await Invalidate();
        _pointPricing = true;
        Assert.True(JsonNode.DeepEquals(await Overview(), changedEntry));

        // Direct definition changes have their own invalidation, independent of the entry PUT.
        details.UnitValue = 125m;
        details.CalculationMethods = [new() { BondDetails = details, DateOperator = DateOperator.UntilDate, DateValue = "2025-01-01", Rate = 0.07m }];
        await context.SaveChangesAsync(ct);
        await Invalidate();
        _pointPricing = false;
        var changed = await Overview();
        Assert.False(JsonNode.DeepEquals(changedEntry["netWorthSeries"], changed["netWorthSeries"]));
        await Invalidate();
        _pointPricing = true;
        Assert.True(JsonNode.DeepEquals(await Overview(), changed));
    }

    private async Task Invalidate()
    {
        using var scope = _scopeFactory!.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ICacheInvalidator>().InvalidateUser(_userId);
    }

    public override void Dispose()
    {
        base.Dispose();
        _database?.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class ReferenceNetWorthService(NetWorthService inner, DashboardLongHeldBondParityTests owner) : INetWorthService
    {
        public Task<decimal?> GetNetWorth(int userId, Currency currency, DateTime date) => inner.GetNetWorth(userId, currency, date);

        public async Task<Dictionary<DateTime, decimal>> GetNetWorth(int userId, Currency currency, DateTime start, DateTime end)
        {
            owner._rangeCalls++;
            if (!owner._pointPricing) return await inner.GetNetWorth(userId, currency, start, end);
            var result = new Dictionary<DateTime, decimal>();
            for (var date = end; date >= start; date = date.AddDays(-1))
                result.Add(date.Date, (await inner.GetNetWorth(userId, currency, date))!.Value); // Keyed by day like the range path.
            return result;
        }
    }
}