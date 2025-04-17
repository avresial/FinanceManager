using ApexCharts;
using FinanceManager.Components.Helpers;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.AccountDetailsPageContents.BankAccountComponents;

public partial class BankAccountDetailsPageContent : ComponentBase
{
    private decimal _balanceChange = 100;
    private bool _loadedAllData = false;
    private DateTime _dateStart;
    private DateTime _dateEnd = DateTime.UtcNow;
    private DateTime? _oldestEntryDate;
    private DateTime? _youngestEntryDate;

    private bool _addEntryVisibility;
    private ApexChart<TimeSeriesModel>? _chart;

    private List<BankAccountEntry>? _top5;
    private List<BankAccountEntry>? _bottom5;
    private string _currency = "PLN";
    private UserSession? _user;
    private ApexChartOptions<TimeSeriesModel> _options = new()
    {
        Chart = new Chart
        {
            Sparkline = new ChartSparkline()
            {
                Enabled = true,
            },
            Toolbar = new Toolbar
            {
                Show = false
            },
        },
        Xaxis = new XAxis()
        {
            AxisTicks = new AxisTicks()
            {
                Show = false,
            },
            AxisBorder = new AxisBorder()
            {
                Show = false
            },
        },
        Yaxis = new List<YAxis>()
        {
            new YAxis
            {
                Show = false,
                SeriesName = "Vaue",
                DecimalsInFloat = 0,
            }
        },
        Colors = new List<string>
        {
           ColorsProvider.GetColors().First()
        }
    };

    public bool IsLoading = false;
    public BankAccount? Account { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<TimeSeriesModel> ChartData { get; set; } = [];

    [Parameter] public required int AccountId { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required ISettingsService settingsService { get; set; }
    [Inject] public required ILoginService loginService { get; set; }

    public async Task ShowOverlay()
    {
        _addEntryVisibility = true;
        StateHasChanged();
        await Task.CompletedTask;
    }
    public async Task HideOverlay()
    {
        _addEntryVisibility = false;
        StateHasChanged();
        await Task.CompletedTask;
    }
    public async Task UpdateInfo()
    {
        if (Account is null || Account.Entries is null) return;

        await UpdateDates();

        if (Account.Entries is not null && Account.Entries.Any() && _oldestEntryDate is not null)
            _loadedAllData = (_oldestEntryDate >= Account.Entries.Last().PostingDate);

        if (Account.Entries is null || Account.Entries.Count == 0) return;

        var EntriesOrdered = Account.Entries.OrderByDescending(x => x.ValueChange);
        _top5 = EntriesOrdered.Take(5).ToList();
        _bottom5 = EntriesOrdered.Skip(Account.Entries.Count - 5)
                                .Take(5)
                                .OrderBy(x => x.ValueChange)
                                .ToList();

        _balanceChange = Account.Entries.First().Value - Account.Entries.Last().Value;

        UpdateChartData();

    }
    public async Task LoadMore()
    {
        if (Account is null || Account.Start is null) return;

        _dateStart = _dateStart.AddMonths(-1);
        if (_user is null) return;

        var newData = await FinancialAccountService.GetAccount<BankAccount>(_user.UserId, AccountId, _dateStart, Account.Start.Value);

        if (Account.Entries is null || newData is null || newData.Entries is null || newData.Entries.Count() == 1)
            return;

        var newEntriesWithoutOldest = newData.Entries.Skip(1);
        Account.Add(newEntriesWithoutOldest, false);
        UpdateChartData();

        if (_chart is not null) await _chart.RenderAsync();
        await UpdateInfo();
    }

    protected override async Task OnInitializedAsync()
    {
        _options.Tooltip = new Tooltip
        {
            Y = new TooltipY
            {
                Formatter = ChartHelper.GetCurrencyFormatter(settingsService.GetCurrency())
            }
        };
        _dateStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        await UpdateEntries();

        AccountDataSynchronizationService.AccountsChanged += AccountDataSynchronizationService_AccountsChanged;
    }
    protected override async Task OnParametersSetAsync()
    {
        IsLoading = true;
        _user = await loginService.GetLoggedUser();
        if (_user is null) return;

        if (_chart is not null)
        {
            if (Account is not null && Account.Entries is not null)
                Account.Entries.Clear();

            await _chart.RenderAsync();
        }

        _loadedAllData = false;
        await UpdateEntries();

        if (_chart is not null)
        {
            StateHasChanged();
            await _chart.RenderAsync();
        }
        IsLoading = false;
    }

    private async Task UpdateEntries()
    {
        try
        {
            var accounts = await FinancialAccountService.GetAvailableAccounts();
            if (accounts.ContainsKey(AccountId))
            {
                var accountType = accounts[AccountId];
                if (accountType == typeof(BankAccount))
                {
                    await UpdateDates();
                    if (_user is not null)
                    {
                        Account = await FinancialAccountService.GetAccount<BankAccount>(_user.UserId, AccountId, _dateStart, DateTime.UtcNow);
                        if (Account is not null && Account.Entries is not null)
                            await UpdateInfo();
                    }
                }
            }

        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        await Task.CompletedTask;
    }
    private void UpdateChartData()
    {
        ChartData.Clear();

        if (Account is null || Account.Entries is null) return;

        decimal previousValue = 0;
        for (DateTime date = _dateStart; date <= _dateEnd; date = date.AddDays(1))
        {
            var entries = Account.Entries.Where(x => x.PostingDate.Date == date.Date);
            var value = entries.Sum(x => x.Value);

            TimeSeriesModel timeSeriesModel = new()
            {
                DateTime = date,
                Value = entries.Any() ? value : previousValue,
            };
            if (entries.Any()) previousValue = value;

            ChartData.Add(timeSeriesModel);
        }
    }
    private async Task UpdateDates()
    {
        _oldestEntryDate = await FinancialAccountService.GetStartDate(AccountId);
        _youngestEntryDate = await FinancialAccountService.GetEndDate(AccountId);

        if (_youngestEntryDate is not null && _dateStart > _youngestEntryDate)
            _dateStart = new DateTime(_youngestEntryDate.Value.Date.Year, _youngestEntryDate.Value.Date.Month, 1);
    }
    private void AccountDataSynchronizationService_AccountsChanged()
    {
        _ = Task.Run(async () =>
          {
              await InvokeAsync(async () =>
              {
                  await UpdateEntries();
                  if (_chart is not null) await _chart.RenderAsync();
                  StateHasChanged();
              });
          });
    }
}