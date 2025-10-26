using FinanceManager.Components.Components.Models;
using FinanceManager.Components.HttpContexts;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.Dashboard.Cards;
public partial class FinancialLabelsListCard
{
    private bool _isLoading;
    private Currency currency = DefaultCurrency.PLN;

    public List<NameValueResult> _data = [];

    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required MoneyFlowHttpContext MoneyFlowHttpContext { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Parameter] public CardMode CardMode { get; set; } = CardMode.List;

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        currency = SettingsService.GetCurrency();
        var userId = await LoginService.GetLoggedUser();

        try
        {
            if (userId is not null)
                _data = await MoneyFlowHttpContext.GetLabelsValue(userId.UserId, StartDateTime, EndDateTime);
        }
        finally
        {
            _isLoading = false;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        _isLoading = true;
        currency = SettingsService.GetCurrency();
        var userId = await LoginService.GetLoggedUser();

        try
        {
            if (userId is not null)
                _data = await MoneyFlowHttpContext.GetLabelsValue(userId.UserId, StartDateTime, EndDateTime);
        }
        finally
        {
            _isLoading = false;
        }
    }


}
