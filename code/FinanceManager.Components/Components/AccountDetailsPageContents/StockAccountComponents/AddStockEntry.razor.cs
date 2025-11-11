using FinanceManager.Components.HttpContexts;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Utilities;

namespace FinanceManager.Components.Components.AccountDetailsPageContents.StockAccountComponents;
public partial class AddStockEntry
{
    private Task<IEnumerable<string>> SearchTicker(string value, CancellationToken token)
    {
        if (string.IsNullOrEmpty(value)) return Task.FromResult(Tickers.AsEnumerable());

        return Task.FromResult(Tickers.Where(x => x.Contains(value, StringComparison.InvariantCultureIgnoreCase)));
    }

    private Currency _currency = DefaultCurrency.PLN;
    private bool success;
    private string[] errors = [];
    private MudForm? form;

    private List<string> InvestmentType = Enum.GetValues(typeof(InvestmentType)).Cast<InvestmentType>().Select(x => x.ToString()).ToList();

    private DateTime? PostingDate = DateTime.Today;
    private TimeSpan? Time = new TimeSpan(01, 00, 00);
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
    [Inject] public required StockPriceHttpContext stockPriceHttpContext { get; set; }

    protected async Task OnFieldChanged(FormFieldChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(Ticker)) return;
        if (!PostingDate.HasValue) return;
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

        var pricePerUnit = await stockPriceHttpContext.GetStockPrice(Ticker, DefaultCurrency.PLN.Id, PostingDate.Value);
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
        if (form is null) return;
        await form.Validate();

        if (!form.IsValid) return;
        if (!BalanceChange.HasValue) return;
        if (!PostingDate.HasValue) return;
        if (!Time.HasValue) return;

        DateTime date = new(PostingDate.Value.Year, PostingDate.Value.Month, PostingDate.Value.Day, Time.Value.Hours, Time.Value.Minutes, Time.Value.Seconds);
        InvestmentType investmentType = Domain.Enums.InvestmentType.Stock;

        try
        {
            investmentType = (InvestmentType)Enum.Parse(typeof(InvestmentType), ExpenseType);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
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
            errors = [ex.ToString()];
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
            return InvestmentType;

        return await Task.FromResult(InvestmentType.Where(x => x.Contains(value, StringComparison.InvariantCultureIgnoreCase)));
    }


}