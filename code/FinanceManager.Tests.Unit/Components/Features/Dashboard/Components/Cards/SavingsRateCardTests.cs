using ApexCharts;
using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards;

[Trait("Category", "Unit")]
public class SavingsRateCardTests
{
    private static readonly DateTime _asOf = new(2026, 7, 20, 13, 45, 30, DateTimeKind.Utc);
    private static readonly string _key = $"savings-rate-card:1:{DefaultCurrency.PLN.Id}:12";
    private readonly Mock<ISnapshotService> _snapshots = new();

    [Fact]
    public async Task VisitFetchesAllTwelveMonths_AndPersistsRenderedValuesWithExactAsOfMetadata()
    {
        var handler = new SavingsRateHandler(_ => Response([new SavingsRate { Salary = 100m, Spending = 25m }]));
        await using var context = CreateContext(handler);
        var card = await RenderAndStartRefresh(context);

        Assert.Equal(12, handler.Count);
        Assert.Equal(new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc), handler.StartDates.Min());
        Assert.Equal(_asOf, handler.EndDates.Max());
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<SavingsRateCardSnapshot>(snapshot =>
            snapshot.UserId == 1 && snapshot.CurrencyId == DefaultCurrency.PLN.Id
            && snapshot.HorizonMonths == 12 && snapshot.AsOfMonth == new DateOnly(2026, 7, 1)
            && snapshot.AsOfDateTime == _asOf && snapshot.Model!.Months.Count == 12
            && snapshot.Model.Months.All(month => month.Salary == 100m && month.Spending == 25m))), Times.Once);
    }

    [Fact]
    public async Task NewAuthenticatedMount_PaintsSnapshotBeforeFreshRequestsAndRefreshesAgainOnNextVisit()
    {
        var asOfMonth = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        StoredForMonth(25m, asOfMonth);
        var firstHandler = new SavingsRateHandler(_ => Response([new SavingsRate { Salary = 100m, Spending = 25m }]), defer: true);
        await using var firstContext = CreateContext(firstHandler);
        var first = RenderAuthenticated(firstContext);
        first.WaitForAssertion(() => Assert.Equal(12, firstHandler.PendingCount));
        Assert.Equal(25m, first.Instance.MonthlySavingsRates[^1].Spending);

        firstHandler.CompleteAll();
        first.WaitForAssertion(() => Assert.False(first.Instance.IsLoading));
        await firstContext.DisposeComponentsAsync();

        var secondHandler = new SavingsRateHandler(_ => Response([new SavingsRate { Salary = 100m, Spending = 25m }]));
        await using var secondContext = CreateContext(secondHandler);
        var second = RenderAuthenticated(secondContext);
        second.WaitForAssertion(() => Assert.False(second.Instance.IsLoading));
        Assert.Equal(12, secondHandler.Count);
    }

    [Fact]
    public async Task EqualRenderedMonths_DoNotWriteWhenCaptureClockAndDecimalScaleDiffer()
    {
        Stored(25.000m);
        var handler = new SavingsRateHandler(_ => Response([new SavingsRate { Salary = 100.00m, Spending = 25.00m }]), defer: true);
        await using var context = CreateContext(handler);
        var card = Render(context);
        Authenticate(context);
        card.Instance.AsOfDate = _asOf;
        var refresh = card.InvokeAsync(() => card.Instance.LoadSavingsRatesAsync());
        card.WaitForAssertion(() => Assert.Equal(12, handler.PendingCount));
        var paintedChart = card.FindComponent<ApexChart<SavingsRateCard.MonthBar>>().Instance;

        handler.CompleteAll();
        await refresh;

        Assert.Equal(12, handler.Count);
        Assert.Same(paintedChart, card.FindComponent<ApexChart<SavingsRateCard.MonthBar>>().Instance);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<SavingsRateCardSnapshot>()), Times.Never);
        Assert.Equal(0.75m, card.Instance.CurrentMonthPercentage);
    }

    [Fact]
    public async Task ChangedRenderedMonth_WritesSnapshot()
    {
        Stored(25m);
        var handler = new SavingsRateHandler(request =>
            ParseStart(request).Month == 7
                ? Response([new SavingsRate { Salary = 100m, Spending = 30m }])
                : Response([new SavingsRate { Salary = 100m, Spending = 25m }]));
        await using var context = CreateContext(handler);
        var card = await RenderAndStartRefresh(context);

        _snapshots.Verify(x => x.SetAsync(_key, It.Is<SavingsRateCardSnapshot>(snapshot =>
            snapshot.Model!.Months[snapshot.Model.Months.Count - 1].Spending == 30m)), Times.Once);
    }

    [Fact]
    public async Task SuccessfulEmptyMonthsBecomeZeroSlotsAndReplaceSnapshot()
    {
        Stored(25m);
        var handler = new SavingsRateHandler(_ => Response([]));
        await using var context = CreateContext(handler);
        var card = await RenderAndStartRefresh(context);

        Assert.Equal(12, handler.Count);
        Assert.All(card.Instance.MonthlySavingsRates, rate =>
        {
            Assert.Equal(0m, rate.Salary);
            Assert.Equal(0m, rate.Spending);
        });
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<SavingsRateCardSnapshot>(snapshot =>
            snapshot.Model!.Months.All(month => month.Salary == 0m && month.Spending == 0m))), Times.Once);
    }

    [Fact]
    public async Task FailedMonthPreservesPaintedSnapshotAndDoesNotWritePartialValues()
    {
        Stored(25m);
        var handler = new SavingsRateHandler((_, index) => index == 0
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : Response([new SavingsRate { Salary = 100m, Spending = 30m }]));
        await using var context = CreateContext(handler);
        var card = await RenderAndStartRefresh(context);

        Assert.Equal(12, handler.Count);
        Assert.Equal(25m, card.Instance.MonthlySavingsRates[^1].Spending);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<SavingsRateCardSnapshot>()), Times.Never);
    }

    [Theory]
    [InlineData("user")]
    [InlineData("currency")]
    [InlineData("horizon")]
    [InlineData("as-of-month")]
    [InlineData("null-month")]
    [InlineData("schema")]
    public async Task InvalidSnapshotScopeOrMonth_IsNotPainted(string invalidField)
    {
        var snapshot = SnapshotForMonth(new DateOnly(2026, 7, 1), 99m);
        switch (invalidField)
        {
            case "user": snapshot.UserId = 2; break;
            case "currency": snapshot.CurrencyId = 2; break;
            case "horizon": snapshot.HorizonMonths = 11; break;
            case "as-of-month": snapshot.AsOfMonth = new DateOnly(2026, 6, 1); break;
            case "null-month":
                var months = snapshot.Model!.Months.ToArray();
                months[0] = null!;
                snapshot.Model = new SavingsRateCardModel(months);
                break;
            case "schema": snapshot.SchemaVersion++; break;
        }
        _snapshots.Setup(x => x.GetAsync<SavingsRateCardSnapshot>(_key)).ReturnsAsync(snapshot);
        var handler = new SavingsRateHandler(_ => Response([new SavingsRate { Salary = 100m, Spending = 30m }]), defer: true);
        await using var context = CreateContext(handler);
        var card = Render(context);
        Authenticate(context);
        card.Instance.AsOfDate = _asOf;
        var refresh = card.InvokeAsync(() => card.Instance.LoadSavingsRatesAsync());
        card.WaitForAssertion(() => Assert.Equal(12, handler.PendingCount));

        Assert.Empty(card.Instance.MonthlySavingsRates);
        handler.CompleteAll();
        await refresh;

        Assert.Equal(30m, card.Instance.MonthlySavingsRates[^1].Spending);
    }

    [Fact]
    public async Task SameScopeSnapshotReadFailureAndFreshFailureRetainsMountedValues()
    {
        var handler = new SavingsRateHandler((_, index) => index < 12
            ? Response([new SavingsRate { Salary = 100m, Spending = 25m }])
            : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        await using var context = CreateContext(handler);
        var card = await RenderAndStartRefresh(context);
        _snapshots.SetupSequence(x => x.GetAsync<SavingsRateCardSnapshot>(_key))
            .ReturnsAsync((SavingsRateCardSnapshot?)null)
            .ThrowsAsync(new InvalidOperationException("storage unavailable"));

        await card.InvokeAsync(() => card.Instance.LoadSavingsRatesAsync());

        Assert.Equal(25m, card.Instance.MonthlySavingsRates[^1].Spending);
        Assert.DoesNotContain("Savings rate unavailable", card.Markup);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<SavingsRateCardSnapshot>()), Times.Once);
    }

    [Fact]
    public async Task NewUserSettingsFailure_ClearsPreviouslyMountedUserData()
    {
        var handler = new SavingsRateHandler(_ => Response([new SavingsRate { Salary = 100m, Spending = 25m }]));
        await using var context = CreateContext(handler);
        var card = await RenderAndStartRefresh(context);
        Mock.Get(context.Services.GetRequiredService<ILoginService>())
            .Setup(x => x.GetLoggedUser())
            .ReturnsAsync(new UserSession { UserId = 2, UserName = "other", Password = "", UserRole = UserRole.User });
        Mock.Get(context.Services.GetRequiredService<ISettingsService>())
            .Setup(x => x.GetCurrencyAsync())
            .ThrowsAsync(new InvalidOperationException("settings unavailable"));

        await card.InvokeAsync(() => card.Instance.LoadSavingsRatesAsync());

        Assert.Empty(card.Instance.MonthlySavingsRates);
        card.WaitForAssertion(() => Assert.Contains("Savings rate unavailable", card.Markup));
    }

    [Fact]
    public async Task CurrencyChange_ClearsOldViewBeforeSnapshotReadCompletes()
    {
        var handler = new SavingsRateHandler((_, index) => index < 12
            ? Response([new SavingsRate { Salary = 100m, Spending = 25m }])
            : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        await using var context = CreateContext(handler);
        var card = await RenderAndStartRefresh(context);
        var usdKey = $"savings-rate-card:1:{DefaultCurrency.USD.Id}:12";
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource<SavingsRateCardSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _snapshots.Setup(x => x.GetAsync<SavingsRateCardSnapshot>(usdKey))
            .Returns(async () =>
            {
                readStarted.TrySetResult();
                return await releaseRead.Task;
            });
        Mock.Get(context.Services.GetRequiredService<ISettingsService>())
            .Setup(x => x.GetCurrencyAsync())
            .ReturnsAsync(DefaultCurrency.USD);
        var refresh = card.InvokeAsync(() => card.Instance.LoadSavingsRatesAsync());
        await readStarted.Task;

        Assert.Empty(card.Instance.MonthlySavingsRates);
        releaseRead.SetResult(null);
        await refresh;

        Assert.Empty(card.Instance.MonthlySavingsRates);
        card.WaitForAssertion(() => Assert.Contains("Savings rate unavailable", card.Markup));
    }

    [Fact]
    public async Task SnapshotWriteFailure_LeavesFreshValuesVisible()
    {
        var handler = new SavingsRateHandler(_ => Response([new SavingsRate { Salary = 100m, Spending = 30m }]));
        await using var context = CreateContext(handler);
        _snapshots.Setup(x => x.SetAsync(_key, It.IsAny<SavingsRateCardSnapshot>()))
            .ThrowsAsync(new InvalidOperationException("storage unavailable"));
        var card = await RenderAndStartRefresh(context);

        Assert.Equal(30m, card.Instance.MonthlySavingsRates[^1].Spending);
        Assert.DoesNotContain("Savings rate unavailable", card.Markup);
        _snapshots.Verify(x => x.SetAsync(_key, It.IsAny<SavingsRateCardSnapshot>()), Times.Once);
    }

    [Fact]
    public async Task SupersededMonthBatch_CannotPaintOrWriteAfterNewerBatch()
    {
        var handler = new SavingsRateHandler((_, index) => Response([
            new SavingsRate { Salary = 100m, Spending = index < 12 ? 25m : 40m },
        ]), defer: true);
        await using var context = CreateContext(handler);
        var card = Render(context);
        Authenticate(context);
        card.Instance.AsOfDate = _asOf;
        var olderRefresh = card.InvokeAsync(() => card.Instance.LoadSavingsRatesAsync());
        card.WaitForAssertion(() => Assert.Equal(12, handler.PendingCount));
        var newerRefresh = card.InvokeAsync(() => card.Instance.LoadSavingsRatesAsync());
        card.WaitForAssertion(() => Assert.Equal(24, handler.PendingCount));

        handler.CompleteRange(12, 24);
        await newerRefresh;
        Assert.Equal(40m, card.Instance.MonthlySavingsRates[^1].Spending);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<SavingsRateCardSnapshot>()), Times.Once);

        handler.CompleteRange(0, 12);
        await olderRefresh;

        Assert.Equal(40m, card.Instance.MonthlySavingsRates[^1].Spending);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<SavingsRateCardSnapshot>()), Times.Once);
    }

    [Fact]
    public async Task NullBodyIsFailureRatherThanAnEmptyMonth()
    {
        var handler = new SavingsRateHandler((_, index) => index == 0
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json") }
            : Response([]));
        await using var context = CreateContext(handler);
        var card = await RenderAndStartRefresh(context);

        card.WaitForAssertion(() => Assert.Contains("Savings rate unavailable", card.Markup));
        Assert.Empty(card.Instance.MonthlySavingsRates);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<SavingsRateCardSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task SelectedMonthSurvivesSnapshotToFreshReconcile()
    {
        Stored(25m);
        var handler = new SavingsRateHandler(_ => Response([new SavingsRate { Salary = 100m, Spending = 30m }]), defer: true);
        await using var context = CreateContext(handler);
        var card = Render(context);
        Authenticate(context);
        card.Instance.AsOfDate = _asOf;
        var refresh = card.InvokeAsync(() => card.Instance.LoadSavingsRatesAsync());
        card.WaitForAssertion(() => Assert.Equal(12, handler.PendingCount));
        card.WaitForAssertion(() => Assert.Equal(12, card.Instance.MonthlySavingsRates.Count));
        Assert.True(card.Instance.SelectMonth(4));
        var selectedMonth = (card.Instance.SelectedMonthRate!.Start.Year, card.Instance.SelectedMonthRate.Start.Month);
        card.Instance.BuildDerivedState();
        card.Render();
        var paintedChart = card.FindComponent<ApexChart<SavingsRateCard.MonthBar>>().Instance;

        handler.CompleteAll();
        await refresh;

        Assert.Equal(selectedMonth, (card.Instance.SelectedMonthRate!.Start.Year, card.Instance.SelectedMonthRate.Start.Month));
        Assert.NotSame(paintedChart, card.FindComponent<ApexChart<SavingsRateCard.MonthBar>>().Instance);
        Assert.Equal(30m, card.Instance.MonthlySavingsRates[^1].Spending);
    }

    [Fact]
    public async Task DisposeDuringRefresh_PreventsLatePaintAndSnapshotWrite()
    {
        var handler = new SavingsRateHandler(_ => Response([new SavingsRate { Salary = 100m, Spending = 25m }]), defer: true);
        await using var context = CreateContext(handler);
        var card = Render(context);
        Authenticate(context);
        card.Instance.AsOfDate = _asOf;
        var refresh = card.InvokeAsync(() => card.Instance.LoadSavingsRatesAsync());
        card.WaitForAssertion(() => Assert.Equal(12, handler.PendingCount));

        await context.DisposeComponentsAsync();
        handler.CompleteAll();
        await refresh;

        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<SavingsRateCardSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task BuildDerivedState_ExcludesSalarylessMonthFromWeightedAverage()
    {
        await using var context = CreateContext(new SavingsRateHandler(_ => Response([])));
        var card = Render(context);
        card.Instance.AsOfDate = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
        card.Instance.MonthlySavingsRates =
        [
            new SavingsRate { Start = new DateTime(2026, 6, 1), Salary = 9456.88m, Spending = 4000m },
            new SavingsRate { Start = new DateTime(2026, 7, 1), Salary = 0m, Spending = 5523.17m },
        ];

        card.Instance.BuildDerivedState();

        Assert.Null(card.Instance.CurrentMonthPercentage);
        Assert.Equal((9456.88m - 4000m) / 9456.88m, card.Instance.YtdAveragePercentage);
        Assert.Null(card.Instance.Series[1].Percentage);
        Assert.Equal((9456.88m - 4000m) / 9456.88m * 100m, card.Instance.Series[0].Percentage);
    }

    [Fact]
    public async Task SelectMonth_UpdatesSelectionAndIgnoresPlaceholderBars()
    {
        await using var context = CreateContext(new SavingsRateHandler(_ => Response([])));
        var card = Render(context);
        var january = new SavingsRate { Start = new DateTime(2026, 1, 1) };
        var february = new SavingsRate { Start = new DateTime(2026, 2, 1) };
        card.Instance.MonthlySavingsRates = [january, february];

        Assert.True(card.Instance.SelectMonth(0));
        Assert.Same(january, card.Instance.SelectedMonthRate);
        Assert.False(card.Instance.SelectMonth(2));
        Assert.Same(january, card.Instance.SelectedMonthRate);
    }

    [Fact]
    public async Task BuildDerivedState_CurrentMonthWithSalary_ReportsRate()
    {
        await using var context = CreateContext(new SavingsRateHandler(_ => Response([])));
        var card = Render(context);
        card.Instance.AsOfDate = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
        var july = new SavingsRate { Start = new DateTime(2026, 7, 1), Salary = 10_000m, Spending = 5000m };
        card.Instance.MonthlySavingsRates = [july];

        card.Instance.BuildDerivedState();

        Assert.Equal(0.5m, card.Instance.CurrentMonthPercentage);
        Assert.Equal(0.5m, card.Instance.YtdAveragePercentage);
        Assert.Equal(50m, card.Instance.Series[0].Percentage);
    }

    [Fact]
    public async Task WeightedAverageAndNegativeRate_UseSalaryWeightsAndKeepNegativeBars()
    {
        await using var context = CreateContext(new SavingsRateHandler(_ => Response([])));
        var card = await RenderAndStartRefresh(context);
        card.Instance.AsOfDate = _asOf;
        card.Instance.MonthlySavingsRates =
        [
            new SavingsRate { Start = new DateTime(2025, 12, 1), Salary = 100m, Spending = 0m },
            new SavingsRate { Start = new DateTime(2026, 6, 1), Salary = 10000m, Spending = 7000m },
            new SavingsRate { Start = new DateTime(2026, 7, 1), Salary = 5000m, Spending = 6000m },
        ];
        card.Instance.SelectMonth(2);
        card.Instance.BuildDerivedState();
        card.Render();

        Assert.Equal(-0.2m, card.Instance.CurrentMonthPercentage);
        Assert.Equal(2000m / 15000m, card.Instance.YtdAveragePercentage);
        Assert.Equal(-20m, card.Instance.Series[2].Percentage);
        Assert.Contains("-20.00", card.Markup);
        Assert.Contains("Saved", card.Markup);
        Assert.Contains("Spent", card.Markup);
        Assert.Contains("About savings rate card", card.Markup);
        await card.Find("button[aria-label='June 2026']").ClickAsync(new());
        Assert.Equal(7000m, card.Instance.SelectedMonthRate!.Spending);
    }

    [Fact]
    public async Task MonthButtons_HaveAccessibleNamesAndExposeSelectedState()
    {
        await using var context = CreateContext(new SavingsRateHandler(_ => Response([
            new SavingsRate { Salary = 100m, Spending = 25m },
        ])));
        var card = await RenderAndStartRefresh(context);

        Assert.Empty(card.FindAll("select[aria-label='Savings rate month']"));
        var buttons = card.FindAll("button.savings-rate-card__month-label");
        Assert.Equal(12, buttons.Count);
        Assert.Equal("July 2026", buttons[^1].GetAttribute("aria-label"));
        Assert.Equal("true", buttons[^1].GetAttribute("aria-pressed"));
        Assert.Equal("false", buttons[0].GetAttribute("aria-pressed"));
        var chart = card.FindComponent<ApexChart<SavingsRateCard.MonthBar>>().Instance;

        await buttons[0].ClickAsync(new());

        Assert.Equal("August 2025", card.Instance.SelectedMonthRate!.Start.ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("true", card.FindAll("button.savings-rate-card__month-label")[0].GetAttribute("aria-pressed"));
        Assert.Same(chart, card.FindComponent<ApexChart<SavingsRateCard.MonthBar>>().Instance);
        Assert.Contains("Salary", card.Markup);
        Assert.Contains("Spent", card.Markup);
        Assert.Contains("Saved", card.Markup);
    }

    [Fact]
    public async Task LatestMonthIsSelectedByDefault_AndMonthWithoutSalaryRemainsSelectableWithoutRate()
    {
        await using var context = CreateContext(new SavingsRateHandler((_, index) => Response(index == 11
            ? [new SavingsRate { Salary = 100m, Spending = 25m }]
            : [])));
        var card = await RenderAndStartRefresh(context);

        Assert.Same(card.Instance.MonthlySavingsRates[^1], card.Instance.SelectedMonthRate);
        Assert.Null(card.Instance.MonthlySavingsRates[0].GetPercentage());
        await card.Find("button[aria-label='August 2025']").ClickAsync(new());
        Assert.Same(card.Instance.MonthlySavingsRates[0], card.Instance.SelectedMonthRate);
        Assert.Null(card.Instance.SelectedMonthRate!.GetPercentage());
        Assert.Null(card.Instance.Series[0].Percentage);
        Assert.True(card.Instance.Series[0].IsSelected);
    }

    [Theory]
    [InlineData("August 2025", "August 2026")]
    [InlineData("June 2026", "June 2026")]
    public async Task RollingMonthWindow_PreservesSelectionWhileAvailableAndSelectsLatestWhenExpired(
        string selectedMonth, string expectedMonth)
    {
        var handler = new SavingsRateHandler(_ => Response([
            new SavingsRate { Salary = 100m, Spending = 25m },
        ]));
        await using var context = CreateContext(handler);
        var card = await RenderAndStartRefresh(context);
        await card.Find($"button[aria-label='{selectedMonth}']").ClickAsync(new());
        card.Instance.AsOfDate = new DateTime(2026, 8, 20, 13, 45, 30, DateTimeKind.Utc);

        await card.InvokeAsync(() => card.Instance.LoadSavingsRatesAsync());

        Assert.Equal(expectedMonth, card.Instance.SelectedMonthRate!.Start.ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task ChartBarSelection_UpdatesMonthWithoutRemountingChart()
    {
        await using var context = CreateContext(new SavingsRateHandler(_ => Response([
            new SavingsRate { Salary = 100m, Spending = 25m },
        ])));
        var card = await RenderAndStartRefresh(context);
        var chart = card.FindComponent<ApexChart<SavingsRateCard.MonthBar>>().Instance;

        await card.InvokeAsync(() => chart.OnDataPointSelection.InvokeAsync(
            new SelectedData<SavingsRateCard.MonthBar> { DataPointIndex = 10 }));

        Assert.Equal("June 2026", card.Instance.SelectedMonthRate!.Start.ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Same(chart, card.FindComponent<ApexChart<SavingsRateCard.MonthBar>>().Instance);
    }

    [Fact]
    public async Task NoSalary_RendersNoRateExplanation()
    {
        await using var context = CreateContext(new SavingsRateHandler(_ => Response([])));
        var card = await RenderAndStartRefresh(context);
        Assert.Contains("No rate", card.Markup);
        Assert.Contains("no salary this month", card.Markup);
        Assert.Null(card.Instance.YtdAveragePercentage);
        Assert.All(card.Instance.Series, bar => Assert.Null(bar.Percentage));
    }

    private void Stored(decimal investmentChange)
    {
        StoredForMonth(investmentChange, new DateOnly(2026, 7, 1));
    }

    private void StoredForMonth(decimal investmentChange, DateOnly asOfMonth)
    {
        _snapshots.Setup(x => x.GetAsync<SavingsRateCardSnapshot>(_key)).ReturnsAsync(SnapshotForMonth(asOfMonth, investmentChange));
    }

    private static SavingsRateCardSnapshot SnapshotForMonth(DateOnly asOfMonth, decimal investmentChange)
    {
        var firstMonth = asOfMonth.AddMonths(-11);
        var model = new SavingsRateCardModel(Enumerable.Range(0, 12)
            .Select(index => new SavingsRateMonthModel(firstMonth.AddMonths(index), 100m, investmentChange)).ToArray());
        return new SavingsRateCardSnapshot
        {
            SchemaVersion = FinanceManager.Components.Shared.Models.SnapshotBase.CurrentSchemaVersion,
            UserId = 1,
            CurrencyId = DefaultCurrency.PLN.Id,
            HorizonMonths = 12,
            AsOfMonth = asOfMonth,
            AsOfDateTime = new DateTime(asOfMonth.Year, asOfMonth.Month, 1, 0, 0, 0, DateTimeKind.Unspecified),
            Model = model,
        };
    }

    private async Task<IRenderedComponent<SavingsRateCard>> RenderAndStartRefresh(BunitContext context)
    {
        var card = Render(context);
        Authenticate(context);
        card.Instance.AsOfDate = _asOf;
        await card.InvokeAsync(() => card.Instance.LoadSavingsRatesAsync());
        Assert.Equal(12, context.Services.GetRequiredService<SavingsRateHandler>().Count);
        return card;
    }

    private BunitContext CreateContext(SavingsRateHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        var settings = new Mock<ISettingsService>();
        settings.Setup(x => x.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        var login = new Mock<ILoginService>();
        login.Setup(x => x.GetLoggedUser()).ReturnsAsync(() => new UserSession { UserId = 1, UserName = "tester", Password = "", UserRole = UserRole.User });
        context.Services.AddSingleton(settings.Object);
        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(new SnapshotRefreshCoordinator(_snapshots.Object, NullLogger<SnapshotRefreshCoordinator>.Instance));
        context.Services.AddSingleton(handler);
        context.Services.AddSingleton(new MoneyFlowHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }));
        return context;
    }

    private static IRenderedComponent<SavingsRateCard> Render(BunitContext context)
    {
        var login = Mock.Get(context.Services.GetRequiredService<ILoginService>());
        login.Setup(x => x.GetLoggedUser()).ReturnsAsync((UserSession?)null);
        return context.Render<SavingsRateCard>();
    }

    private static IRenderedComponent<SavingsRateCard> RenderAuthenticated(BunitContext context) =>
        context.Render<SavingsRateCard>();

    private static void Authenticate(BunitContext context) =>
        Mock.Get(context.Services.GetRequiredService<ILoginService>())
            .Setup(x => x.GetLoggedUser())
            .ReturnsAsync(() => new UserSession { UserId = 1, UserName = "tester", Password = "", UserRole = UserRole.User });

    private static HttpResponseMessage Response(List<SavingsRate> rates) => new(HttpStatusCode.OK) { Content = JsonContent.Create(rates) };

    private static DateTime ParseStart(HttpRequestMessage request)
    {
        var encoded = request.RequestUri!.Query.Split('&').Single(part => part.StartsWith("start=", StringComparison.Ordinal))[6..];
        return DateTime.Parse(Uri.UnescapeDataString(encoded), null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    private sealed class SavingsRateHandler(
        Func<HttpRequestMessage, int, HttpResponseMessage> response,
        bool defer = false) : HttpMessageHandler
    {
        private readonly List<(TaskCompletionSource<HttpResponseMessage> Completion, HttpResponseMessage Response)> _pending = [];
        private readonly object _sync = new();
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public int PendingCount { get { lock (_sync) return _pending.Count; } }
        public List<DateTime> StartDates { get; } = [];
        public List<DateTime> EndDates { get; } = [];

        public SavingsRateHandler(Func<HttpRequestMessage, HttpResponseMessage> response, bool defer = false)
            : this((request, _) => response(request), defer) { }

        public void CompleteAll()
            => CompleteRange(0, Count);

        public void CompleteRange(int startIndex, int endIndex)
        {
            lock (_sync)
                foreach (var pending in _pending.Skip(startIndex).Take(endIndex - startIndex))
                    pending.Completion.TrySetResult(pending.Response);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = Interlocked.Increment(ref _count) - 1;
            var responseMessage = response(request, index);
            var end = request.RequestUri!.Query.Split('&').Single(part => part.StartsWith("end=", StringComparison.Ordinal))[4..];
            lock (_sync)
            {
                StartDates.Add(ParseStart(request));
                EndDates.Add(DateTime.Parse(Uri.UnescapeDataString(end), null, System.Globalization.DateTimeStyles.RoundtripKind));
            }
            if (!defer) return Task.FromResult(responseMessage);

            var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_sync) _pending.Add((completion, responseMessage));
            return completion.Task;
        }
    }
}