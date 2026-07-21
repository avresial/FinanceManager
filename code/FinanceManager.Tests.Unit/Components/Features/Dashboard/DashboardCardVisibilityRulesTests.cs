using FinanceManager.Components.Components.Features.Dashboard.Models;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard;

[Trait("Category", "Unit")]
public class DashboardCardVisibilityRulesTests
{
    [Fact]
    public void Liabilities_WithNoData_IsAutoHidden()
    {
        var overview = new DashboardOverviewDto();

        Assert.True(DashboardCardVisibilityRules.IsAutoHiddenWhenEmpty(DashboardCards.Liabilities, overview));
    }

    [Fact]
    public void Liabilities_WithData_IsNotAutoHidden()
    {
        var overview = new DashboardOverviewDto
        {
            LiabilitiesPerType = [new NameValueResult { Name = "Loan", Value = -1200 }],
        };

        Assert.False(DashboardCardVisibilityRules.IsAutoHiddenWhenEmpty(DashboardCards.Liabilities, overview));
    }

    [Fact]
    public void Liabilities_WithOnlyZeroValues_IsAutoHidden()
    {
        var overview = new DashboardOverviewDto
        {
            LiabilitiesPerType = [new NameValueResult { Name = "Loan", Value = 0 }],
        };

        Assert.True(DashboardCardVisibilityRules.IsAutoHiddenWhenEmpty(DashboardCards.Liabilities, overview));
    }

    [Fact]
    public void Assets_WithAccountDataButNoTypeData_IsNotAutoHidden()
    {
        var overview = new DashboardOverviewDto
        {
            AssetsPerAccount = [new NameValueResult { Name = "Wallet", Value = 500 }],
        };

        Assert.False(DashboardCardVisibilityRules.IsAutoHiddenWhenEmpty(DashboardCards.Assets, overview));
    }

    [Theory]
    [InlineData(DashboardCards.NetWorth)]
    [InlineData(DashboardCards.NetCashFlow)]
    [InlineData(DashboardCards.ClosingBalance)]
    [InlineData(DashboardCards.Insights)]
    [InlineData(DashboardCards.RecurringTransactions)]
    public void NonDataBackedCards_AreNeverAutoHidden(string cardId)
    {
        var overview = new DashboardOverviewDto();

        Assert.False(DashboardCardVisibilityRules.IsAutoHiddenWhenEmpty(cardId, overview));
    }

    [Fact]
    public void Expenses_And_Labels_WithNoData_AreAutoHidden()
    {
        var overview = new DashboardOverviewDto();

        Assert.True(DashboardCardVisibilityRules.IsAutoHiddenWhenEmpty(DashboardCards.Expenses, overview));
        Assert.True(DashboardCardVisibilityRules.IsAutoHiddenWhenEmpty(DashboardCards.Labels, overview));
    }
}