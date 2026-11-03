using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Shared.Charting;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.Shared;

/// <summary>Series colours for the account details hero chart.</summary>
public static class AccountChartColors
{
    public const string DefaultBalanceColor = BalanceChartColors.DarkAccent;

    /// <summary>
    /// Loan balances are debt, so they use the same colour as the dashboard's Liabilities card.
    /// </summary>
    public static string BalanceColor(AccountLabel accountType) =>
        accountType == AccountLabel.Loan ? ColorsProvider.LiabilityColor : DefaultBalanceColor;
}