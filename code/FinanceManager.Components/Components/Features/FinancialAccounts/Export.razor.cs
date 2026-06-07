using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FinanceManager.Components.Components.Features.FinancialAccounts;

public partial class Export : ComponentBase
{
    [Parameter] public required int AccountId { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }

    [Inject] public required HttpClient HttpClient { get; set; }
    [Inject] public required IJSRuntime JSRuntime { get; set; }
    [Inject] public required CurrencyAccountHttpClient CurrencyAccountHttpClient { get; set; }
    [Inject] public required StockAccountHttpClient StockAccountHttpClient { get; set; }
    [Inject] public required BondAccountHttpClient BondAccountHttpClient { get; set; }

    public FinancialAccountTypeDescriptor? AccountDescriptor { get; private set; }
    public string AccountName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;

    private DateTime? _startDate;
    private DateTime? _endDate;
    private DateTime? _availableStartDate;
    private DateTime? _availableEndDate;
    private bool _isLoading = true;
    private bool _isDownloading;

    private bool HasAvailableDateRange => _availableStartDate.HasValue && _availableEndDate.HasValue;
    private bool CanDownload => HasAvailableDateRange && _startDate.HasValue && _endDate.HasValue && _startDate.Value.Date <= _endDate.Value.Date;

    protected override async Task OnParametersSetAsync()
    {
        AccountDescriptor = null;
        AccountName = string.Empty;
        ErrorMessage = string.Empty;
        _startDate = null;
        _endDate = null;
        _availableStartDate = null;
        _availableEndDate = null;
        _isLoading = true;

        try
        {
            await UpdateAccountType();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task UpdateAccountType()
    {
        try
        {
            var accounts = await FinancialAccountService.GetAvailableAccounts();
            if (accounts.TryGetValue(AccountId, out var accountType))
            {
                AccountDescriptor = FinancialAccountTypeDescriptor.FromType(accountType);
                if (AccountDescriptor is null)
                {
                    ErrorMessage = $"{accountType} type is not supported.";
                    return;
                }

                await UpdateAccountName();
                await UpdateAvailableDateRange();
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
        if (AccountDescriptor?.Kind == FinancialAccountKind.Currency)
        {
            var account = await CurrencyAccountHttpClient.GetAccountAsync(AccountId);
            AccountName = account?.Name ?? string.Empty;
            return;
        }

        if (AccountDescriptor?.Kind == FinancialAccountKind.Stock)
        {
            var account = await StockAccountHttpClient.GetAccountAsync(AccountId);
            AccountName = account?.Name ?? string.Empty;
            return;
        }

        if (AccountDescriptor?.Kind == FinancialAccountKind.Bond)
        {
            var account = await BondAccountHttpClient.GetAccountAsync(AccountId);
            AccountName = account?.Name ?? string.Empty;
        }
    }

    private async Task UpdateAvailableDateRange()
    {
        var startDate = await FinancialAccountService.GetStartDate(AccountId);
        var endDate = await FinancialAccountService.GetEndDate(AccountId);

        if (!startDate.HasValue || !endDate.HasValue)
            return;

        _availableStartDate = NormalizePickerDate(startDate.Value);
        _availableEndDate = NormalizePickerDate(endDate.Value);
    }

    private static DateTime NormalizePickerDate(DateTime dateTime) =>
        dateTime.Kind == DateTimeKind.Utc ? dateTime.ToLocalTime().Date : dateTime.Date;

    private string GetAccountTypeLabel()
    {
        return AccountDescriptor?.Label ?? "Unknown";
    }

    private string? GetExportEndpoint()
    {
        return AccountDescriptor?.GetExportEndpoint(AccountId);
    }

    private async Task DownloadSelected()
    {
        if (!CanDownload)
        {
            ErrorMessage = "Please provide a valid date range.";
            return;
        }

        await Download(_startDate!.Value, _endDate!.Value);
    }

    private async Task DownloadAll()
    {
        if (!HasAvailableDateRange)
        {
            ErrorMessage = "This account has no entries available to export.";
            return;
        }

        await Download(_availableStartDate!.Value, _availableEndDate!.Value);
    }

    private async Task Download(DateTime selectedStartDate, DateTime selectedEndDate)
    {
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
            // Interpret selected dates as local calendar days and convert to UTC start/end-of-day.
            var localStart = DateTime.SpecifyKind(selectedStartDate.Date, DateTimeKind.Local);
            var localEnd = DateTime.SpecifyKind(selectedEndDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local);
            var startDate = localStart.ToUniversalTime();
            var endDate = localEnd.ToUniversalTime();

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