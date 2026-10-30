using Bunit;
using FinanceManager.Components.Features.FinancialAccounts.Components.CurrencyAccountComponents.TransactionHistory;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Components.CurrencyAccountComponents;

[Trait("Category", "Unit")]
public class CurrencyDayGroupCardTests
{
    private static readonly DateTime _day = new(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
    private static readonly CurrencyAccount _account = new(1, 1, "Cash");

    [Fact]
    public async Task SameEntriesInANewSequence_DoNotRerenderTheDay()
    {
        await using var context = CreateContext();
        CurrencyAccountEntry[] entries = [Entry(1, -20m), Entry(2, -5m)];
        var cut = Render(context, entries);
        var renders = cut.RenderCount;

        // The page regroups on every render, so the card gets a new sequence holding the same entries.
        cut.Render(p => p.Add(x => x.Entries, entries.ToList()));

        Assert.Equal(renders, cut.RenderCount);
    }

    [Fact]
    public async Task ChangedEntries_RerenderTheDay()
    {
        await using var context = CreateContext();
        var cut = Render(context, [Entry(1, -20m), Entry(2, -5m)]);

        cut.Render(p => p.Add(x => x.Entries, new[] { Entry(1, -20m), Entry(2, -7m) }));

        Assert.Contains("-27.00", cut.Markup.Replace(",", "."));
        Assert.Contains("2 entries", cut.Markup);
    }

    private static CurrencyAccountEntry Entry(int id, decimal change) =>
        new(1, id, _day.AddHours(12), 100m, change) { Description = $"Entry {id}" };

    private static IRenderedComponent<CurrencyDayGroupCard> Render(BunitContext context, IEnumerable<CurrencyAccountEntry> entries) =>
        context.Render<CurrencyDayGroupCard>(p => p
            .Add(x => x.Day, _day)
            .Add(x => x.Account, _account)
            .Add(x => x.Entries, entries)
            .Add(x => x.Currency, "PLN"));

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton(Mock.Of<IFinancialAccountService>());
        context.Services.AddSingleton(new AccountDataSynchronizationService());
        context.Services.AddSingleton(NullLogger<TransactionRow>.Instance);
        return context;
    }
}