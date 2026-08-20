using FinanceManager.Components.Features.FinancialAccounts.Components.Shared;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Components.Shared;

[Trait("Category", "Unit")]
public class AccountHistoryToolbarTests
{
    private static readonly DateTime _customStart = new(2026, 1, 5);
    private static readonly DateTime _customEnd = new(2026, 6, 15);

    [Theory]
    [InlineData("Month")]
    [InlineData("1M")]
    [InlineData("3M")]
    [InlineData("6M")]
    [InlineData("YTD")]
    public void IsCustomRange_IsFalse_ForEachPresetRange(string preset)
    {
        var toolbar = CreateToolbar(selectedRange: preset);

        Assert.False(toolbar.IsCustomRange);
    }

    [Theory]
    [InlineData("Month")]
    [InlineData("1M")]
    [InlineData("3M")]
    [InlineData("6M")]
    [InlineData("YTD")]
    public void RangeButtonText_ReturnsExactPresetLabel_ForEachPresetRange(string preset)
    {
        var toolbar = CreateToolbar(selectedRange: preset);

        Assert.Equal(preset, toolbar.RangeButtonText);
    }

    [Theory]
    [InlineData("Month")]
    [InlineData("1M")]
    [InlineData("3M")]
    [InlineData("6M")]
    [InlineData("YTD")]
    public void RangeButtonAccessibleText_KeepsExactPresetLabel_ForEachPresetRange(string preset)
    {
        var toolbar = CreateToolbar(selectedRange: preset);

        Assert.Equal($"Date range: {preset}", toolbar.RangeButtonAccessibleText);
    }

    [Fact]
    public void IsCustomRange_IsTrue_WhenCustomRangeIsSelected()
    {
        var toolbar = CreateToolbar(
            selectedRange: AccountHistoryToolbar.CustomRangeKey,
            customDateRange: new DateRange(_customStart, _customEnd));

        Assert.True(toolbar.IsCustomRange);
    }

    [Fact]
    public void IsCustomRange_IsTrue_WhenCustomRangeIsSelectedButDatesAreMissing()
    {
        var toolbar = CreateToolbar(selectedRange: AccountHistoryToolbar.CustomRangeKey);

        Assert.True(toolbar.IsCustomRange);
    }

    [Fact]
    public void RangeButtonAccessibleText_SpellsOutBothDates_WhenCustomRangeIsComplete()
    {
        var toolbar = CreateToolbar(
            selectedRange: AccountHistoryToolbar.CustomRangeKey,
            customDateRange: new DateRange(_customStart, _customEnd));

        Assert.Equal("Date range: 05/01/2026 to 15/06/2026", toolbar.RangeButtonAccessibleText);
    }

    [Fact]
    public void RangeButtonText_KeepsShortDatedForm_WhenCustomRangeIsComplete()
    {
        var toolbar = CreateToolbar(
            selectedRange: AccountHistoryToolbar.CustomRangeKey,
            customDateRange: new DateRange(_customStart, _customEnd));

        Assert.Equal("05/01 – 15/06", toolbar.RangeButtonText);
    }

    [Fact]
    public void RangeButtonText_UsesShortForm_WhenCustomRangeIsCompleteAndMobile()
    {
        var toolbar = CreateToolbar(
            selectedRange: AccountHistoryToolbar.CustomRangeKey,
            customDateRange: new DateRange(_customStart, _customEnd),
            isMobile: true);

        Assert.Equal("Custom", toolbar.RangeButtonText);
    }

    [Fact]
    public void RangeButtonAccessibleText_FallsBackToHonestCustom_WhenCustomDatesAreMissing()
    {
        var toolbar = CreateToolbar(selectedRange: AccountHistoryToolbar.CustomRangeKey);

        Assert.Equal("Date range: custom range", toolbar.RangeButtonAccessibleText);
    }

    [Fact]
    public void RangeButtonAccessibleText_FallsBackToHonestCustom_WhenOnlyOneCustomDateIsPresent()
    {
        var toolbar = CreateToolbar(
            selectedRange: AccountHistoryToolbar.CustomRangeKey,
            customDateRange: new DateRange(_customStart, end: null));

        Assert.Equal("Date range: custom range", toolbar.RangeButtonAccessibleText);
    }

    [Fact]
    public void RangeButtonText_FallsBackToCustom_WhenCustomDatesAreMissing()
    {
        var toolbar = CreateToolbar(selectedRange: AccountHistoryToolbar.CustomRangeKey);

        Assert.Equal("Custom", toolbar.RangeButtonText);
    }

    private static AccountHistoryToolbar CreateToolbar(
        string? selectedRange = null,
        DateRange? customDateRange = null,
        bool isMobile = false)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(AccountHistoryToolbar.CustomDateRange)] = customDateRange,
            [nameof(AccountHistoryToolbar.IsMobile)] = isMobile
        };

        if (selectedRange is not null)
            parameters[nameof(AccountHistoryToolbar.SelectedRange)] = selectedRange;

#pragma warning disable BL0005 // Required component parameter is intentionally seeded before renderer-free unit assertions.
        var toolbar = new AccountHistoryToolbar { AccountId = 1 };
#pragma warning restore BL0005
        ParameterView.FromDictionary(parameters).SetParameterProperties(toolbar);
        return toolbar;
    }
}