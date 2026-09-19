using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Alerts.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards;

public partial class FinancialAlertsCardView
{
    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public bool HasError { get; set; }
    [Parameter] public int ConfiguredCount { get; set; }
    [Parameter] public IReadOnlyList<AlertEvaluationOutcome> Triggered { get; set; } = [];
    [Parameter] public AlertEvaluationOutcome? SelectedAlert { get; set; }
    [Parameter] public bool IsLoadingDetail { get; set; }
    [Parameter] public bool HasDetailError { get; set; }
    [Parameter] public EventCallback<AlertEvaluationOutcome> OnAlertSelected { get; set; }
    [Parameter] public EventCallback OnBack { get; set; }

    private async Task HandleAlertKeyDown(KeyboardEventArgs eventArgs, AlertEvaluationOutcome alert)
    {
        if (eventArgs.Key is "Enter" or " ")
            await OnAlertSelected.InvokeAsync(alert);
    }

    private static string GetEmptyDetailMessage(AlertEvaluationOutcome alert) =>
        alert.AlertType is AlertType.AccountBalance or AlertType.SubscriptionPriceChange
            ? alert.Message
            : "No matching transactions found for this alert.";
}