using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Globalization;

namespace FinanceManager.Components.Components.Dashboard.Cards;

public partial class FinancialLabelsListCard
{
    private bool _isLoading;
    private bool _hasError;
    private Currency _currency = DefaultCurrency.PLN;

    public List<NameValueResult> _data = [];

    private List<NameValueResult> _income = [];
    private List<NameValueResult> _spending = [];
    private decimal _totalIn;
    private decimal _totalOut;
    private decimal _net;
    private decimal _maxIn = 1m;
    private decimal _maxOut = 1m;

    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required MoneyFlowHttpClient MoneyFlowHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Parameter] public CardMode CardMode { get; set; } = CardMode.List;

    protected override Task OnParametersSetAsync() => LoadData();

    private async Task LoadData()
    {
        _isLoading = true;
        _hasError = false;
        _currency = SettingsService.GetCurrency();
        var userId = await LoginService.GetLoggedUser();

        try
        {
            if (userId is not null)
                _data = (await MoneyFlowHttpClient.GetLabelsValue(userId.UserId, StartDateTime, EndDateTime)).Where(x => x.Value != 0).ToList();

            BuildGroups();
        }
        catch
        {
            _hasError = true;
            Snackbar.Add("Unable to load financial labels.", Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void BuildGroups()
    {
        _income = _data.Where(x => x.Value > 0).OrderByDescending(x => x.Value).ToList();
        _spending = _data.Where(x => x.Value < 0).OrderByDescending(x => Math.Abs(x.Value)).ToList();
        _totalIn = _income.Sum(x => x.Value);
        _totalOut = _spending.Sum(x => Math.Abs(x.Value));
        _net = _totalIn - _totalOut;
        _maxIn = _income.Count > 0 ? _income.Max(x => x.Value) : 1m;
        _maxOut = _spending.Count > 0 ? _spending.Max(x => Math.Abs(x.Value)) : 1m;
    }

    // Signed money — space thousands, dot decimal, currency suffix. Tabular figures applied in markup.
    private string Fmt(decimal value, bool showPlus = false)
    {
        var body = Math.Abs(value).ToString("#,##0.00", CultureInfo.InvariantCulture).Replace(",", " ");
        var prefix = value < 0 ? "-" : (showPlus && value > 0 ? "+" : string.Empty);
        return $"{prefix}{body} {_currency.ShortName}";
    }

    private static double BarPercentage(decimal value, decimal groupMax) =>
        groupMax == 0 ? 0 : (double)(Math.Abs(value) / groupMax * 100);

    private static int SharePercentage(decimal value, decimal groupTotal) =>
        groupTotal == 0 ? 0 : (int)Math.Round(Math.Abs(value) / groupTotal * 100);

    private string GetCategoryIcon(string categoryName) => categoryName.ToLower() switch
    {
        var n when n.Contains("salary") => Icons.Material.Filled.Paid,
        var n when n.Contains("freelance") => Icons.Material.Filled.Work,
        var n when n.Contains("income") => Icons.Material.Filled.TrendingUp,
        var n when n.Contains("dining") || n.Contains("restaurant") || n.Contains("food") => Icons.Material.Filled.Restaurant,
        var n when n.Contains("grocery") || n.Contains("groceries") => Icons.Material.Filled.ShoppingCart,
        var n when n.Contains("healthcare") || n.Contains("health") || n.Contains("medical") => Icons.Material.Filled.LocalHospital,
        var n when n.Contains("utilities") || n.Contains("electric") || n.Contains("water") => Icons.Material.Filled.Bolt,
        var n when n.Contains("transport") || n.Contains("gas") || n.Contains("taxi") || n.Contains("car") => Icons.Material.Filled.DirectionsCar,
        var n when n.Contains("entertainment") || n.Contains("movie") || n.Contains("game") => Icons.Material.Filled.Movie,
        var n when n.Contains("travel") || n.Contains("hotel") || n.Contains("flight") => Icons.Material.Filled.Flight,
        var n when n.Contains("shopping") || n.Contains("clothes") => Icons.Material.Filled.ShoppingBag,
        _ => Icons.Material.Filled.Label
    };
}
