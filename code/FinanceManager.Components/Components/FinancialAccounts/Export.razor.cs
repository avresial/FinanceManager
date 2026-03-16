using FinanceManager.Components.Services;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FinanceManager.Components.Components.FinancialAccounts;

public partial class Export : ComponentBase
{
    [Parameter] public required int AccountId { get; set; }

    [Inject] public required IFinancialAccountService FinancalAccountService { get; set; }
    [Inject] public required HttpClient HttpClient { get; set; }
    [Inject] public required IJSRuntime JSRuntime { get; set; }
    [Inject] public required CurrencyAccountHttpClient CurrencyAccountHttpClient { get; set; }
    [Inject] public required StockAccountHttpClient StockAccountHttpClient { get; set; }
    [Inject] public required BondAccountHttpClient BondAccountHttpClient { get; set; }

    public Type? accountType = null;
    public string AccountName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;

    private DateTime? _startDate;
    private DateTime? _endDate;
    private bool _isDownloading;

    private bool CanDownload => _startDate.HasValue && _endDate.HasValue && _startDate.Value <= _endDate.Value;

    protected override async Task OnInitializedAsync()
    {
        SetDefaultDates();
        await UpdateAccountType();
    }

    protected override async Task OnParametersSetAsync()
    {
        accountType = null;
        AccountName = string.Empty;
        ErrorMessage = string.Empty;
        SetDefaultDates();
        await UpdateAccountType();
    }

    private void SetDefaultDates()
    {
        var now = DateTime.UtcNow;
        _startDate = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        _endDate = now.Date;
    }

    private async Task UpdateAccountType()
    {
        try
        {
            var accounts = await FinancalAccountService.GetAvailableAccounts();
            if (accounts.ContainsKey(AccountId))
            {
                accountType = accounts[AccountId];
                await UpdateAccountName();
            }
            else
                ErrorMessage = $"Account {AccountId} was not found.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task UpdateAccountName()
    {
        if (accountType == typeof(CurrencyAccount))
        {
            var account = await CurrencyAccountHttpClient.GetAccountAsync(AccountId);
            AccountName = account?.Name ?? string.Empty;
            return;
        }

        if (accountType == typeof(StockAccount))
        {
            var account = await StockAccountHttpClient.GetAccountAsync(AccountId);
            AccountName = account?.Name ?? string.Empty;
            return;
        }

        if (accountType == typeof(BondAccount))
        {
            var account = await BondAccountHttpClient.GetAccountAsync(AccountId);
            AccountName = account?.Name ?? string.Empty;
        }
    }

    private string GetAccountTypeLabel()
    {
        if (accountType == typeof(CurrencyAccount)) return "Currency";
        if (accountType == typeof(StockAccount)) return "Stock";
        if (accountType == typeof(BondAccount)) return "Bond";

        return "Unknown";
    }

    private string? GetExportEndpoint()
    {
        if (accountType == typeof(CurrencyAccount)) return $"api/CurrencyAccount/export/{AccountId}";
        if (accountType == typeof(StockAccount)) return $"api/StockAccount/export/{AccountId}";
        if (accountType == typeof(BondAccount)) return $"api/BondAccount/export/{AccountId}";

        return null;
    }

    private async Task Download()
    {
        if (!CanDownload)
        {
            ErrorMessage = "Please provide a valid date range.";
            return;
        }

        var endpoint = GetExportEndpoint();
        if (endpoint is null)
        {
            ErrorMessage = "This account type is not supported for export.";
            return;
        }

        _isDownloading = true;
        ErrorMessage = string.Empty;

        try
        {
            var startDate = DateTime.SpecifyKind(_startDate!.Value, DateTimeKind.Utc);
            var endDate = DateTime.SpecifyKind(_endDate!.Value, DateTimeKind.Utc);

            var url = $"{endpoint}?startDate={Uri.EscapeDataString(startDate.ToString("O"))}&endDate={Uri.EscapeDataString(endDate.ToString("O"))}";
            var response = await HttpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = await response.Content.ReadAsStringAsync();
                return;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes.Length == 0)
            {
                ErrorMessage = "No data to export for selected range.";
                return;
            }

            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                           ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                           ?? $"export-{AccountId}-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.csv";

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "text/csv";

            await JSRuntime.InvokeVoidAsync("financeManager.downloadFileFromBase64", fileName, contentType, Convert.ToBase64String(bytes));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            _isDownloading = false;
        }
    }
}