using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Users;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.Dashboard.Cards;

public partial class FinancialInsightsCarousel
{
    private bool _isLoading;
    private List<FinancialInsight> _insights = [];

    [Inject] public required FinancialInsightsHttpClient FinancialInsightsHttpClient { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public int Count { get; set; } = 3;
    [Parameter] public int? AccountId { get; set; }

    protected override async Task OnParametersSetAsync() => await LoadInsightsAsync();

    private async Task LoadInsightsAsync()
    {
        _isLoading = true;
        try
        {
            _insights = await FinancialInsightsHttpClient.GetLatestAsync(Count, AccountId);
        }
        catch
        {
            Snackbar.Add("Unable to load financial insights.", Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }
}