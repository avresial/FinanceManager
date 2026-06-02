using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards;

public partial class FinancialLabelsListCard
{
    private bool _isLoading;
    private Currency _currency = DefaultCurrency.PLN;

    public List<NameValueResult> _data = [];

    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required MoneyFlowHttpClient MoneyFlowHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Parameter] public CardMode CardMode { get; set; } = CardMode.List;

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        _currency = SettingsService.GetCurrency();
        var userId = await LoginService.GetLoggedUser();

        try
        {
            if (userId is not null)
                _data = (await MoneyFlowHttpClient.GetLabelsValue(userId.UserId, StartDateTime, EndDateTime)).Where(x => x.Value != 0).ToList();
        }
        catch
        {
            Snackbar.Add("Unable to load financial labels.", Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        _isLoading = true;
        _currency = SettingsService.GetCurrency();
        var userId = await LoginService.GetLoggedUser();

        try
        {
            if (userId is not null)
                _data = (await MoneyFlowHttpClient.GetLabelsValue(userId.UserId, StartDateTime, EndDateTime)).Where(x => x.Value != 0).ToList();
        }
        catch
        {
            Snackbar.Add("Unable to load financial labels.", Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private double GetPercentage(decimal value)
    {
        if (_data.Count == 0) return 0;
        var maxValue = _data.Max(x => Math.Abs(x.Value));
        return maxValue == 0 ? 0 : (double)(Math.Abs(value) / maxValue * 100);
    }

    private string GetCategoryIcon(string categoryName) => categoryName.ToLower() switch
    {
        var n when n.Contains("salary") => Icons.Material.Filled.Paid,
        var n when n.Contains("income") => Icons.Material.Filled.TrendingUp,
        var n when n.Contains("dining") || n.Contains("restaurant") || n.Contains("food") => Icons.Material.Filled.Restaurant,
        var n when n.Contains("grocery") || n.Contains("groceries") => Icons.Material.Filled.ShoppingCart,
        var n when n.Contains("healthcare") || n.Contains("health") || n.Contains("medical") => Icons.Material.Filled.LocalHospital,
        var n when n.Contains("utilities") || n.Contains("electric") || n.Contains("water") => Icons.Material.Filled.Bolt,
        var n when n.Contains("transport") || n.Contains("gas") || n.Contains("taxi") || n.Contains("car") => Icons.Material.Filled.DirectionsCar,
        var n when n.Contains("entertainment") || n.Contains("movie") || n.Contains("game") => Icons.Material.Filled.Movie,
        var n when n.Contains("travel") || n.Contains("hotel") || n.Contains("flight") => Icons.Material.Filled.Flight,
        var n when n.Contains("shopping") || n.Contains("clothes") => Icons.Material.Filled.ShoppingBag,
        _ => Icons.Material.Filled.LocalAtm
    };
}