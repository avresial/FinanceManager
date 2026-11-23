using FinanceManager.Application.Alerts.Models;
using FinanceManager.Components.Features.Alerts.Components;
using FinanceManager.Domain.Alerts.Dtos;
using FinanceManager.Domain.Alerts.Enums;

namespace FinanceManager.Tests.Unit.Components.Features.Alerts;

[Trait("Category", "Unit")]
public sealed class AlertPresentationTests
{
    [Theory]
    [InlineData(AlertComparisonOperator.GreaterThan, ">")]
    [InlineData(AlertComparisonOperator.GreaterThanOrEqual, ">=")]
    [InlineData(AlertComparisonOperator.LessThan, "<")]
    [InlineData(AlertComparisonOperator.LessThanOrEqual, "<=")]
    [InlineData(AlertComparisonOperator.Equal, "=")]
    [InlineData(AlertComparisonOperator.NotEqual, "!=")]
    public void ComparisonSymbol_UsesUserFacingOperators(AlertComparisonOperator comparison, string expected)
    {
        Assert.Equal(expected, AlertPresentation.ComparisonSymbol(comparison));
    }

    [Fact]
    public void TransactionLinkFiltersTheTransactionLogByDescriptionAndCurrency()
    {
        var transaction = new AlertTransactionReference(
            EntryId: 34,
            PostingDate: new DateTime(2026, 9, 2),
            Amount: 7895m,
            Description: "Card payment",
            ContractorDetails: "Example Store",
            CurrencyCode: "PLN");

        Assert.Equal("/Transactions?search=Card%20payment&currency=PLN", AlertPresentation.TransactionHref(transaction));
        Assert.Equal("Example Store · 2 Sep 2026", AlertPresentation.TransactionLabel(transaction));
    }

    [Fact]
    public void TransactionLink_FallsBackToContractorThenToTheLog()
    {
        var withContractor = new AlertTransactionReference(1, new DateTime(2026, 9, 2), 5m, " ", "Example Store");
        var withoutText = new AlertTransactionReference(2, new DateTime(2026, 9, 2), 5m, "", null);

        Assert.Equal("/Transactions?search=Example%20Store", AlertPresentation.TransactionHref(withContractor));
        Assert.Equal("/Transactions", AlertPresentation.TransactionHref(withoutText));
    }

    [Fact]
    public void TypeLabel_NamesTheBalanceAlertCashBalance()
    {
        Assert.Equal("Cash balance", AlertPresentation.TypeLabel(AlertType.AccountBalance));
    }

    [Fact]
    public void ConditionLabel_ShowsCurrencyOrMarksItRequired()
    {
        var withCurrency = Alert(AlertType.LargeTransaction, currencyId: 0);
        var migrated = Alert(AlertType.AccountBalance, currencyId: null);
        var subscription = Alert(AlertType.SubscriptionPriceChange, currencyId: null);

        Assert.EndsWith("PLN", AlertPresentation.ConditionLabel(withCurrency, "PLN"));
        Assert.EndsWith("(currency required)", AlertPresentation.ConditionLabel(migrated));
        Assert.DoesNotContain("currency", AlertPresentation.ConditionLabel(subscription));
        Assert.True(AlertPresentation.NeedsCurrency(migrated));
        Assert.False(AlertPresentation.NeedsCurrency(withCurrency));
        Assert.False(AlertPresentation.NeedsCurrency(subscription));
    }

    private static FinancialAlertDto Alert(AlertType type, int? currencyId) =>
        new(Guid.NewGuid(), 1, "Alert", type, false, AlertComparisonOperator.GreaterThan, 100m, AlertEvaluationPeriod.CurrentMonth,
            currencyId, null, null, null, null, AlertTriggerStatus.Healthy, null, null, DateTime.UtcNow, null);
}