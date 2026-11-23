using FinanceManager.Components.Features.Alerts.Models;
using FinanceManager.Domain.Alerts.Dtos;
using FinanceManager.Domain.Alerts.Enums;

namespace FinanceManager.Tests.Unit.Components.Features.Alerts;

[Trait("Category", "Unit")]
public class AlertFormModelTests
{
    [Fact]
    public void ToCreateCommand_SendsOnlyTheTargetFieldsOfTheSelectedType()
    {
        var form = new AlertFormModel
        {
            Title = "  Groceries  ",
            AlertType = AlertType.CategorySpending,
            CurrencyId = 7,
            LabelName = " Food ",
            MerchantName = "Lidl",
        };

        var command = form.ToCreateCommand();

        Assert.Equal("Groceries", command.Title);
        Assert.Equal(7, command.CurrencyId);
        Assert.Equal("Food", command.LabelName);
        Assert.Null(command.MerchantName);
    }

    [Theory]
    [InlineData(AlertType.AccountBalance, true)]
    [InlineData(AlertType.CategorySpending, true)]
    [InlineData(AlertType.MerchantSpending, true)]
    [InlineData(AlertType.LargeTransaction, true)]
    [InlineData(AlertType.SubscriptionPriceChange, false)]
    public void TargetCurrencyId_OnlyForCashAlertTypes(AlertType type, bool requiresCurrency)
    {
        var form = new AlertFormModel { AlertType = type, CurrencyId = 3 };

        Assert.Equal(requiresCurrency, form.RequiresCurrency);
        Assert.Equal(requiresCurrency ? 3 : null, form.TargetCurrencyId);
    }

    [Fact]
    public void HasValidScope_RequiresACurrencyForCashAlertsOnly()
    {
        var form = new AlertFormModel { AlertType = AlertType.LargeTransaction, CurrencyId = null };
        Assert.False(form.HasValidScope);

        form.CurrencyId = 0; // PLN has id 0 and is a valid choice
        Assert.True(form.HasValidScope);

        form.CurrencyId = null;
        form.AlertType = AlertType.SubscriptionPriceChange;
        Assert.True(form.HasValidScope);
    }

    [Fact]
    public void FromDto_MigratedAlertWithoutCurrency_KeepsTheCurrencyBlank()
    {
        var dto = new FinancialAlertDto(Guid.NewGuid(), 1, "Old", AlertType.AccountBalance, false,
            AlertComparisonOperator.LessThan, 100m, AlertEvaluationPeriod.CurrentMonth, null, null, null, null, null,
            AlertTriggerStatus.Healthy, null, null, DateTime.UtcNow, null);

        var form = AlertFormModel.FromDto(dto);

        Assert.Null(form.CurrencyId);
        Assert.False(form.HasValidScope);
    }

    [Fact]
    public void ToUpdateCommand_BlankMerchant_IsNull()
    {
        var form = new AlertFormModel { Title = "Shop", AlertType = AlertType.MerchantSpending, MerchantName = "   ", IsEnabled = false };

        var command = form.ToUpdateCommand();

        Assert.Null(command.MerchantName);
        Assert.False(command.IsEnabled);
    }
}