using FinanceManager.Application.Alerts.Models;
using FinanceManager.Components.Features.Alerts.HttpClients;
using FinanceManager.Domain.Alerts.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards;

public partial class FinancialAlertsCard
{
    private bool _isLoading = true;
    private bool _hasError;
    private List<FinancialAlertDto> _alerts = [];
    private List<AlertEvaluationOutcome> _triggered = [];

    [Parameter] public string Height { get; set; } = "300px";

    [Inject] public required FinancialAlertsHttpClient FinancialAlertsHttpClient { get; set; }
    [Inject] public required ILogger<FinancialAlertsCard> Logger { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var outcomes = await FinancialAlertsHttpClient.EvaluateAsync();
            _triggered = outcomes.Where(x => x.IsTriggered).OrderByDescending(x => x.IsNewlyTriggered).ThenBy(x => x.AlertTitle).ToList();
            _alerts = await FinancialAlertsHttpClient.GetAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to load dashboard financial alerts");
            _hasError = true;
        }
        finally
        {
            _isLoading = false;
        }
    }
}