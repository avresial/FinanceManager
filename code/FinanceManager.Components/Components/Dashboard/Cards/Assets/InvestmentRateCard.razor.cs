using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards.Assets;

public partial class InvestmentRateCard
{
    private bool _isLoading;
    private List<InvestmentRate> _investmentRates { get; set; } = [];

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;


    [Inject] public required ILogger<InvestmentRateCard> Logger { get; set; }
    [Inject] public required IMoneyFlowService MoneyFlowService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        _investmentRates.Clear();

        var user = await LoginService.GetLoggedUser();
        if (user is null) return;

        try
        {
            _investmentRates = await MoneyFlowService.GetInvestmentRate(user.UserId, StartDateTime, EndDateTime).ToListAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while getting net worth");
        }

    }
}