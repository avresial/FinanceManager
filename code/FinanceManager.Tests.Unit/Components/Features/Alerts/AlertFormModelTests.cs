using FinanceManager.Components.Features.Alerts.Models;
using FinanceManager.Domain.Alerts.Enums;

namespace FinanceManager.Tests.Unit.Components.Features.Alerts;

[Trait("Category", "Unit")]
public class AlertFormModelTests
{
    [Fact]
    public void ToCreateCommand_SendsOnlyTheTargetOfTheSelectedType()
    {
        var form = new AlertFormModel
        {
            Title = "  Groceries  ",
            AlertType = AlertType.CategorySpending,
            AccountId = 7,
            LabelName = " Food ",
            MerchantName = "Lidl",
        };

        var command = form.ToCreateCommand();

        Assert.Equal("Groceries", command.Title);
        Assert.Null(command.AccountId);
        Assert.Equal("Food", command.LabelName);
        Assert.Null(command.MerchantName);
    }

    [Fact]
    public void TargetAccountId_OnlyForAccountBalance()
    {
        var form = new AlertFormModel { AlertType = AlertType.AccountBalance, AccountId = 3 };
        Assert.Equal(3, form.TargetAccountId);

        form.AlertType = AlertType.LargeTransaction;
        Assert.Null(form.TargetAccountId);
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