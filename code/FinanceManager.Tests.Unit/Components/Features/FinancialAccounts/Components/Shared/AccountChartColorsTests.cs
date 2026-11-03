using FinanceManager.Components.Features.FinancialAccounts.Components.Shared;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Shared.Charting;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Components.Shared;

[Trait("Category", "Unit")]
public class AccountChartColorsTests
{
    [Fact]
    public void LoanAccountUsesLiabilityColour() =>
        Assert.Equal(ColorsProvider.LiabilityColor, AccountChartColors.BalanceColor(AccountLabel.Loan));

    [Theory]
    [InlineData(AccountLabel.Cash)]
    [InlineData(AccountLabel.Stock)]
    [InlineData(AccountLabel.Bond)]
    [InlineData(AccountLabel.RealEstate)]
    public void NonDebtAccountsKeepTheAccentColour(AccountLabel accountType) =>
        Assert.Equal(AccountChartColors.DefaultBalanceColor, AccountChartColors.BalanceColor(accountType));
}