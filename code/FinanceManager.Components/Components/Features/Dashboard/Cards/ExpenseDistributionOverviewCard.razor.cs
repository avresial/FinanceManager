using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Components.Features.Dashboard.Models;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.Dashboard.Cards;

public partial class ExpenseDistributionOverviewCard
{
    private bool _isLoading;
    private Currency _currency = DefaultCurrency.PLN;
    private List<NameValueResult> _data = [];

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    // When the dashboard supplies a prepared model the card renders it directly;
    // otherwise it self-loads from the API as in standalone usage.
    [Parameter] public NameValueListCardModel? Model { get; set; }

    [Inject] public required ILogger<ExpenseDistributionOverviewCard> Logger { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required MoneyFlowHttpClient MoneyFlowHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override void OnInitialized()
    {
        _currency = SettingsService.GetCurrency();
    }

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            if (Model is not null)
            {
                _data = [.. Model.Items];
                return;
            }

            var user = await LoginService.GetLoggedUser();
            if (user is null)
            {
                _data = [];
                return;
            }

            var data = await MoneyFlowHttpClient.GetExpenseDistribution(user.UserId, _currency, StartDateTime, EndDateTime);
            _data = [.. data];
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ex.Message);
            Snackbar.Add("Unable to load expense distribution.", Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }
}