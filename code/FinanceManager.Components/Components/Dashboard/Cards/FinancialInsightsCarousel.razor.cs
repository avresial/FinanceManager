using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.Dashboard.Cards;

public partial class FinancialInsightsCarousel
{
    private bool _isLoading;
    private List<FinancialInsight> _insights = [];

    [Inject] public required FinancialInsightsHttpClient FinancialInsightsHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public int Count { get; set; } = 3;
    [Parameter] public int? AccountId { get; set; }

    protected override async Task OnInitializedAsync() => await LoadInsightsAsync();

    protected override async Task OnParametersSetAsync() => await LoadInsightsAsync();

    private async Task LoadInsightsAsync()
    {
        _isLoading = true;
        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null)
            {
                _insights = [];
                return;
            }

            _insights = await FinancialInsightsHttpClient.GetLatestAsync(Count, AccountId);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static IEnumerable<string> GetTags(string tags) =>
        tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}