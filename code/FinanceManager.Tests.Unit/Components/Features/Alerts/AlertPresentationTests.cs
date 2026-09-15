using FinanceManager.Application.Alerts.Models;
using FinanceManager.Components.Features.Alerts.Components;
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
    public void TransactionLinkPointsToExistingAccountDetailsAndEntry()
    {
        var transaction = new AlertTransactionReference(
            AccountId: 12,
            EntryId: 34,
            PostingDate: new DateTime(2026, 9, 2),
            Amount: 7895m,
            Description: "Card payment",
            ContractorDetails: "Example Store");

        Assert.Equal("/AccountDetails/12?entryId=34", AlertPresentation.TransactionHref(transaction));
        Assert.Equal("Example Store · 2026-09-02", AlertPresentation.TransactionLabel(transaction));
    }
}