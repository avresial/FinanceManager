using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;
using MudBlazor.Utilities;

namespace FinanceManager.Components.Components.FinancialAccounts.StockAccountComponents.Crud;

public partial class AddStockEntry
{
    private Task<IEnumerable<string>> SearchTicker(string value, CancellationToken token)
    {
        if (string.IsNullOrEmpty(value)) return Task.FromResult(Tickers.AsEnumerable());

        return Task.FromResult(Tickers.Where(x => x.Contains(value, StringComparison.InvariantCultureIgnoreCase)));
    }

    private Currency _currency = DefaultCurrency.PLN;
    private bool _success;
    private string[] _errors = [];
    private MudForm? _form;

    private List<string> _investmentType = Enum.GetValues(typeof(InvestmentType)).Cast<InvestmentType>().Select(x => x.ToString()).ToList();

    private DateTime? _postingDate = DateTime.Today;
    private TimeSpan? _time = new TimeSpan(01, 00, 00);
    public string ExpenseType { get; set; } = FinanceManager.Domain.Enums.InvestmentType.Stock.ToString();
    public string Ticker { get; set; } = string.Empty;
    public decimal? BalanceChange;
    public decimal? PricePerUnit { get; set; } = null;

    [Parameter] public List<string> Tickers { get; set; } = [];
    [Parameter] public RenderFragment? CustomButton { get; set; }
    [Parameter] public Func<Task>? ActionCompleted { get; set; }
    [Parameter] public required StockAccount InvestmentAccount { get; set; }

    [Inject] public required IFinancialAccountService FinancalAccountService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required StockPriceHttpClient StockPriceHttpClient { get; set; }
    [Inject] public required ILogger<AddStockEntry> Logger { get; set; }

    protected async Task OnFieldChanged(FormFieldChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(Ticker)) return;
        if (!_postingDate.HasValue) return;
        if (args.Field is MudTextField<decimal?> textField)
        {
            if (textField is null || textField.Label is null) return;
            if (!textField.Label.ToLower().Contains("ticker")) return;
        }

        if (args.Field is MudNumericField<decimal?> numericField)
        {
            if (numericField is null || numericField.Label is null) return;
            if (!numericField.Label.ToLower().Contains("change")) return;
        }

        var pricePerUnit = await StockPriceHttpClient.GetStockPrice(Ticker, DefaultCurrency.PLN.Id, _postingDate.Value);
        if (pricePerUnit is null) return;

        PricePerUnit = BalanceChange * pricePerUnit.PricePerUnit;
    }
    protected override Task OnParametersSetAsync()
    {
        _currency = SettingsService.GetCurrency();
        return base.OnParametersSetAsync();
    }

    public async Task Add()
    {
        if (_form is null) return;
        await _form.Validate();

        if (!_form.IsValid) return;
        if (!BalanceChange.HasValue) return;
        if (!_postingDate.HasValue) return;
        if (!_time.HasValue) return;

        DateTime date = new(_postingDate.Value.Year, _postingDate.Value.Month, _postingDate.Value.Day, _time.Value.Hours, _time.Value.Minutes, _time.Value.Seconds);
        InvestmentType investmentType = Domain.Enums.InvestmentType.Stock;

        try
        {
            investmentType = (InvestmentType)Enum.Parse(typeof(InvestmentType), ExpenseType);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error parsing investment type '{ExpenseType}'.", ExpenseType);
        }

        var id = 0;
        var currentMaxId = InvestmentAccount.GetMaxId();
        if (currentMaxId is not null)
            id += currentMaxId.Value + 1;

        StockAccountEntry entry = new(InvestmentAccount.AccountId, id, date.ToUniversalTime(), -1, BalanceChange.Value, Ticker, investmentType);

        try
        {
            await FinancalAccountService.AddEntry(entry);
            InvestmentAccount.Add(entry);
        }
        catch (Exception ex)
        {
            _errors = [ex.ToString()];
        }

        if (ActionCompleted is not null)
            await ActionCompleted();
    }

    public async Task Cancel()
    {
        if (ActionCompleted is not null)
            await ActionCompleted();
    }

    private async Task<IEnumerable<string>> Search(string value, CancellationToken token)
    {
        if (string.IsNullOrEmpty(value))
            return _investmentType;

        return await Task.FromResult(_investmentType.Where(x => x.Contains(value, StringComparison.InvariantCultureIgnoreCase)));
    }


}