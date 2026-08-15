using FinanceManager.Components.Features.Dashboard.Components.Pages;
using System.Reflection;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Pages;

[Trait("Category", "Unit")]
public class OverviewPagesInitialRangeTests
{
    // The Assets and Liabilities pages must open on the window the Dashboard seeds — a rolling
    // 31 days anchored to midnight — instead of the current calendar month, which is nearly
    // empty at the start of a month. #700
    [Fact]
    public void AssetsPage_InitializesWithDefaultOverviewRange()
    {
        var page = new AssetsPage();

        AssertDefaultOverviewRange(page, () => page.StartDate, () => page.EndDate);
    }

    [Fact]
    public void LiabilitiesPage_InitializesWithDefaultOverviewRange()
    {
        var page = new LiabilitiesPage();

        AssertDefaultOverviewRange(page, () => page.StartDate, () => page.EndDate);
    }

    private static void AssertDefaultOverviewRange(object page, Func<DateTime> startDate, Func<DateTime> endDate)
    {
        var before = DateTime.UtcNow;
        Initialize(page);
        var after = DateTime.UtcNow;

        // Asserted as the relationship the shared helper defines rather than against a captured
        // "now", so the expectation holds even if the clock crosses midnight mid-test.
        Assert.InRange(endDate(), before, after);
        Assert.Equal(endDate().Date.AddDays(-30), startDate());
        Assert.Equal(DateTimeKind.Utc, startDate().Kind);
    }

    // The pages are never rendered here, so lifecycle is driven directly; OnInitialized is
    // protected by ComponentBase's contract, hence the reflection.
    private static void Initialize(object page) =>
        page.GetType()
            .GetMethod("OnInitialized", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(page, null);
}