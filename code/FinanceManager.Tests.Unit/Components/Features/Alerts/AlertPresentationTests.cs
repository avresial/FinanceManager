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

    private static readonly DateTime _now = new(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TransactionLinkFiltersTheTransactionLogByDescriptionAndCurrency()
    {
        var transaction = new AlertTransactionReference(
            EntryId: 34,
            PostingDate: new DateTime(2026, 11, 2),
            Amount: 7895m,
            Description: "Card payment",
            ContractorDetails: "Example Store",
            CurrencyCode: "PLN");

        Assert.Equal("/Transactions?search=Card%20payment&currency=PLN&range=Month", AlertPresentation.TransactionHref(transaction, _now));
        Assert.Equal("Example Store · 2 Nov 2026", AlertPresentation.TransactionLabel(transaction));
    }

    [Fact]
    public void TransactionLink_FallsBackToContractorThenToJustTheRange()
    {
        var withContractor = new AlertTransactionReference(1, new DateTime(2026, 11, 2), 5m, " ", "Example Store");
        var withoutText = new AlertTransactionReference(2, new DateTime(2026, 11, 2), 5m, "", null);

        Assert.Equal("/Transactions?search=Example%20Store&range=Month", AlertPresentation.TransactionHref(withContractor, _now));
        Assert.Equal("/Transactions?range=Month", AlertPresentation.TransactionHref(withoutText, _now));
    }

    // The log opens on three months unless told otherwise, so every link must reach back to the posting date.
    [Theory]
    [InlineData("2026-11-02", "range=Month")]
    [InlineData("2026-10-30", "range=1M")]
    [InlineData("2026-09-10", "range=3M")]
    [InlineData("2026-07-01", "range=6M")]
    [InlineData("2026-03-01", "range=YTD")]
    [InlineData("2025-12-31", "from=2025-12-31")]
    [InlineData("2024-02-29", "from=2024-02-29")]
    public void TransactionLink_ReachesBackToThePostingDate(string postingDate, string expectedRange)
    {
        var transaction = new AlertTransactionReference(1, DateTime.Parse(postingDate, System.Globalization.CultureInfo.InvariantCulture), 5m, "Rent", null, "PLN");

        Assert.EndsWith($"&{expectedRange}", AlertPresentation.TransactionHref(transaction, _now));
    }

    [Fact]
    public void ComparisonDetail_AppendsTheCurrencyCodeToBothSides()
    {
        var outcome = new AlertEvaluationOutcome(
            Guid.NewGuid(), "Low cash", AlertType.AccountBalance, AlertTriggerStatus.Triggered,
            true, _now, false, DeDuplicationReason.None, 50m, 100m,
            AlertComparisonOperator.LessThan, "x", "m", _now, new Dictionary<string, string> { ["CurrencyCode"] = "EUR" });

        Assert.Equal("50.00 EUR < 100.00 EUR", AlertPresentation.ComparisonDetail(outcome));
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

    [Fact]
    public void NeedsCurrency_AlsoCoversAStoredCurrencyThatIsNotInTheCatalog()
    {
        var orphaned = Alert(AlertType.AccountBalance, currencyId: 999);
        IReadOnlySet<int> catalog = new HashSet<int> { 0, 1 };

        Assert.True(AlertPresentation.NeedsCurrency(orphaned, catalog));
        Assert.False(AlertPresentation.NeedsCurrency(orphaned));
        Assert.False(AlertPresentation.NeedsCurrency(Alert(AlertType.AccountBalance, currencyId: 0), catalog));
    }

    [Fact]
    public void Status_DistinguishesAMigratedAlertThatNeedsACurrencyFromADisabledOne()
    {
        var migrated = Alert(AlertType.AccountBalance, currencyId: null);
        var disabled = Alert(AlertType.AccountBalance, currencyId: 0);
        var enabled = disabled with { IsEnabled = true };

        Assert.Equal("Needs currency", AlertPresentation.StatusLabel(migrated, null));
        Assert.Equal(MudBlazor.Color.Warning, AlertPresentation.StatusColor(migrated, null));
        Assert.Equal("Disabled", AlertPresentation.StatusLabel(disabled, null));
        Assert.Equal(MudBlazor.Color.Default, AlertPresentation.StatusColor(disabled, null));
        Assert.Equal("Healthy", AlertPresentation.StatusLabel(enabled, null));
        Assert.Equal(MudBlazor.Color.Success, AlertPresentation.StatusColor(enabled, null));
    }

    private static FinancialAlertDto Alert(AlertType type, int? currencyId) =>
        new(Guid.NewGuid(), 1, "Alert", type, false, AlertComparisonOperator.GreaterThan, 100m, AlertEvaluationPeriod.CurrentMonth,
            currencyId, null, null, null, null, AlertTriggerStatus.Healthy, null, null, DateTime.UtcNow, null);
}