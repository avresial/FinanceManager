using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Components.Features.Dashboard.Models;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.Features.Dashboard.Cards;

public partial class FinancialLabelsListCard
{
    private bool _isLoading;
    private bool _hasError;
    private Currency _currency = DefaultCurrency.PLN;

    public List<NameValueResult> _data = [];

    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required MoneyFlowHttpClient MoneyFlowHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Parameter] public CardMode CardMode { get; set; } = CardMode.List;

    // When the dashboard supplies a prepared model the card renders it directly;
    // otherwise it self-loads from the API as in standalone usage.
    [Parameter] public NameValueListCardModel? Model { get; set; }

    protected override Task OnParametersSetAsync() => LoadData();

    private async Task LoadData()
    {
        _isLoading = true;
        _hasError = false;
        _currency = SettingsService.GetCurrency();

        if (Model is not null)
        {
            _data = Model.Items.Where(x => x.Value != 0).ToList();
            _isLoading = false;
            return;
        }

        var userId = await LoginService.GetLoggedUser();

        try
        {
            if (userId is not null)
                _data = (await MoneyFlowHttpClient.GetLabelsValue(userId.UserId, StartDateTime, EndDateTime)).Where(x => x.Value != 0).ToList();
        }
        catch
        {
            _hasError = true;
        }
        finally
        {
            _isLoading = false;
        }
    }
}