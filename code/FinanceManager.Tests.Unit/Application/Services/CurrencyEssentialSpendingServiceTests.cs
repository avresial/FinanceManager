using FinanceManager.Application.MoneyFlow.Spending;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class CurrencyEssentialSpendingServiceTests
{
    private readonly InMemoryOwnerLedger _ledger = new();
    private readonly Mock<IFinancialLabelsRepository> _labelsMock = new();
    private readonly CurrencyEssentialSpendingService _service;
    private readonly DateTime _date = new(2026, 3, 10);
    private readonly FinancialLabel _essential = CreateLabel(1, "Rent", FinancialLabelClassificationCatalog.EssentialValue);
    private readonly FinancialLabel _want = CreateLabel(2, "Dining", FinancialLabelClassificationCatalog.WantValue);

    public CurrencyEssentialSpendingServiceTests()
    {
        _labelsMock.Setup(x => x.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { _essential, _want }.ToAsyncEnumerable());
        _service = new CurrencyEssentialSpendingService(_ledger, _labelsMock.Object);
    }

    [Fact]
    public async Task GetEssentialSpending_IncludesOnlyResolvedEssentialOutflows()
    {
        _ledger.Add(0, _date, -100m, [_essential.Id])
            .Add(0, _date, -50m, [_essential.Id, _want.Id, _essential.Id])
            .Add(0, _date, -10m, [_essential.Id, _want.Id])
            .Add(0, _date, 100m, [_essential.Id]);

        var result = await _service.GetEssentialSpending(1, DefaultCurrency.PLN, _date, _date);

        Assert.Single(result);
        Assert.Equal(-150m, result[0].Value);
    }

    [Fact]
    public async Task GetEssentialSpending_TieResolution_ExcludesEntry()
    {
        _ledger.Add(0, _date, -10m, [_essential.Id, _want.Id]);

        var result = await _service.GetEssentialSpending(1, DefaultCurrency.PLN, _date, _date);

        Assert.Single(result);
        Assert.Equal(0m, result[0].Value);
    }

    [Fact]
    public async Task GetEssentialSpending_GuestOwnerUnlinkedEntries_FillsEveryDayOfTheWindow()
    {
        _ledger.Add(0, _date.AddDays(1), -30m, [_essential.Id]);

        var result = await _service.GetEssentialSpending(-5, DefaultCurrency.PLN, _date, _date.AddDays(2));

        Assert.Equal(3, result.Count);
        Assert.Equal(-30m, result.Sum(x => x.Value));
    }

    [Fact]
    public async Task GetEssentialSpending_NoEntriesInWindow_ReturnsEmpty()
    {
        _ledger.Add(0, _date.AddDays(-30), -30m, [_essential.Id]);

        Assert.Empty(await _service.GetEssentialSpending(1, DefaultCurrency.PLN, _date, _date.AddDays(2)));
    }

    [Fact]
    public async Task GetEssentialSpending_LocalBoundsCrossingUtcMidnight_DoNotThrowAndKeepBoundaryEntry()
    {
        // 2026-03-10 00:30 at UTC+1 is 2026-03-09 23:30Z: the entry's UTC date is before the raw start date.
        var startLocal = DateTime.SpecifyKind(new DateTime(2026, 3, 10, 0, 30, 0), DateTimeKind.Unspecified);
        var start = new DateTimeOffset(startLocal, TimeSpan.FromHours(1)).LocalDateTime;
        var startUtc = start.ToUniversalTime();
        _ledger.Add(0, startUtc.AddMinutes(10), -25m, [_essential.Id]);

        var result = await _service.GetEssentialSpending(1, DefaultCurrency.PLN, start, start.AddDays(2));

        Assert.Equal(-25m, result.Sum(x => x.Value));
    }

    [Fact]
    public async Task GetEssentialSpending_UnspecifiedBounds_AreTreatedAsUtc()
    {
        _ledger.Add(0, _date, -25m, [_essential.Id]);

        var result = await _service.GetEssentialSpending(1, DefaultCurrency.PLN, DateTime.SpecifyKind(_date, DateTimeKind.Unspecified), DateTime.SpecifyKind(_date, DateTimeKind.Unspecified));

        Assert.Equal(-25m, result.Sum(x => x.Value));
    }

    private static FinancialLabel CreateLabel(int id, string name, string value) =>
        new()
        {
            Id = id,
            Name = name,
            Classifications =
            [
                new FinancialLabelClassification
                {
                    Kind = FinancialLabelClassificationCatalog.SpendingNecessityKind,
                    Value = value
                }
            ]
        };
}