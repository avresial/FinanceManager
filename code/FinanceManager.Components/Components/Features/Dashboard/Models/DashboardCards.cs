namespace FinanceManager.Components.Components.Features.Dashboard.Models;

/// <summary>Stable identifier and display title for a dashboard card the user can show or hide.</summary>
public sealed record DashboardCardDescriptor(string Id, string Title);

/// <summary>
/// Registry of the dashboard cards that participate in show/hide customization.
/// The <see cref="Id"/> values are persisted in the user's visibility preferences,
/// so they must stay stable; renaming a card's title is safe, changing its id is not.
/// </summary>
public static class DashboardCards
{
    public const string NetWorth = "net-worth";
    public const string NetCashFlow = "net-cash-flow";
    public const string ClosingBalance = "closing-balance";
    public const string Liabilities = "liabilities";
    public const string Labels = "labels";
    public const string Assets = "assets";
    public const string Expenses = "expenses";
    public const string Insights = "insights";
    public const string RecurringTransactions = "recurring-transactions";

    /// <summary>All customizable cards in the order they appear on the dashboard.</summary>
    public static readonly IReadOnlyList<DashboardCardDescriptor> All =
    [
        new(NetWorth, "Net worth"),
        new(NetCashFlow, "Net cash flow"),
        new(ClosingBalance, "Closing balance"),
        new(Liabilities, "Liabilities"),
        new(Labels, "Financial labels"),
        new(Assets, "Assets"),
        new(Expenses, "Expenses"),
        new(Insights, "Financial insights"),
        new(RecurringTransactions, "Recurring transactions"),
    ];
}