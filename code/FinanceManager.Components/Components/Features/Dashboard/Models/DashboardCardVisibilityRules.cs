using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Components.Features.Dashboard.Models;

/// <summary>
/// Decides whether a card should be automatically hidden because it has no data for the
/// loaded period (e.g. a user with no liabilities should not see an empty liabilities card).
/// This is orthogonal to the user's explicit show/hide preference: a card is rendered only
/// when the user has not hidden it <em>and</em> it is not auto-hidden as empty here.
/// </summary>
public static class DashboardCardVisibilityRules
{
    /// <summary>
    /// True when <paramref name="cardId"/> is a data-backed card whose data in
    /// <paramref name="overview"/> is empty and should therefore collapse. Cards that are not
    /// data-backed (net worth, insights, recurring transactions) are never auto-hidden.
    /// </summary>
    public static bool IsAutoHiddenWhenEmpty(string cardId, DashboardOverviewDto overview) => cardId switch
    {
        DashboardCards.Liabilities => IsEmpty(overview.LiabilitiesPerType) && IsEmpty(overview.LiabilitiesPerAccount),
        DashboardCards.Assets => IsEmpty(overview.AssetsPerType) && IsEmpty(overview.AssetsPerAccount),
        DashboardCards.Expenses => IsEmpty(overview.ExpenseDistribution),
        DashboardCards.Labels => IsEmpty(overview.LabelsValue),
        _ => false,
    };

    private static bool IsEmpty(IEnumerable<NameValueResult> data) => !data.Any(x => x.Value != 0);
}