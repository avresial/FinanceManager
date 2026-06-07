using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Utilities;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.StockAccountComponents.Crud;

public partial class UpdateStockEntry
{
    private int? _loadedEntryId;
    private Currency _currency = DefaultCurrency.PLN;
    private MudForm? _form;
    private readonly List<string> _investmentTypes = Enum.GetValues(typeof(InvestmentType)).Cast<InvestmentType>().Select(x => x.ToString()).ToList();

    private DateTime? _postingDate = DateTime.Today;
    private TimeSpan? _time = new(01, 00, 00);
    private string _investmentTypeName = FinanceManager.Domain.Enums.InvestmentType.Stock.ToString();
    private string _isin = string.Empty;
    private decimal? _balanceChange;
    private decimal? _pricePerUnit;

    [Parameter] public EventCallback ActionCompleted { get; set; }
    [Parameter] public required StockAccount StockAccount { get; set; }
    [Parameter] public required StockAccountEntry StockEntry { get; set; }

    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required StockPriceHttpClient StockPriceHttpClient { get; set; }
    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }

    protected async Task OnFieldChanged(FormFieldChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(_isin)) return;
        if (!_postingDate.HasValue) return;
        if (args.Field is MudTextField<decimal?> textField)
        {
            if (textField.Label is null) return;
            if (!textField.Label.Contains("isin", StringComparison.OrdinalIgnoreCase)) return;
        }

        if (args.Field is MudNumericField<decimal?> numericField)
        {
            if (numericField.Label is null) return;
            if (!numericField.Label.Contains("change", StringComparison.OrdinalIgnoreCase)) return;
        }

        await UpdatePricePerUnit();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedEntryId.HasValue && _loadedEntryId.Value == StockEntry.EntryId) return;
        _loadedEntryId = StockEntry.EntryId;

        _currency = SettingsService.GetCurrency();
        _postingDate = StockEntry.PostingDate.ToLocalTime();
        _time = new TimeSpan(_postingDate.Value.Hour, _postingDate.Value.Minute, _postingDate.Value.Second);
        _investmentTypeName = StockEntry.InvestmentType.ToString();
        _isin = StockEntry.Isin;
        _balanceChange = StockEntry.ValueChange;

        await UpdatePricePerUnit();
    }

    public async Task Update()
    {
        if (_form is null) return;

        await _form.Validate();
        if (!_form.IsValid) return;
        if (!_balanceChange.HasValue) return;
        if (!_postingDate.HasValue) return;
        if (!_time.HasValue) return;

        var date = new DateTime(
            _postingDate.Value.Year,
            _postingDate.Value.Month,
            _postingDate.Value.Day,
            _time.Value.Hours,
            _time.Value.Minutes,
            _time.Value.Seconds,
            DateTimeKind.Local).ToUniversalTime();

        var investmentType = Enum.Parse<InvestmentType>(_investmentTypeName);
        StockAccountEntry stockEntry = new(StockEntry.AccountId, StockEntry.EntryId, date, -1, _balanceChange.Value, _isin, investmentType);

        StockAccount.UpdateEntry(stockEntry);
        await FinancialAccountService.UpdateEntry(stockEntry);
        await ActionCompleted.InvokeAsync();
    }

    public async Task Cancel()
    {
        await ActionCompleted.InvokeAsync();
    }

    private async Task UpdatePricePerUnit()
    {
        if (_postingDate is null) return;

        var pricePerUnit = await StockPriceHttpClient.GetStockPrice(_isin, DefaultCurrency.PLN.Id, _postingDate.Value.Date.ToUniversalTime());
        if (pricePerUnit is null) return;

        _pricePerUnit = _balanceChange * pricePerUnit.PricePerUnit;
    }

    private Task<IEnumerable<string>> SearchInvestmentTypes(string value, CancellationToken token)
    {
        if (string.IsNullOrEmpty(value))
            return Task.FromResult(_investmentTypes.AsEnumerable());

        return Task.FromResult(_investmentTypes.Where(x => x.Contains(value, StringComparison.InvariantCultureIgnoreCase)));
    }
}
