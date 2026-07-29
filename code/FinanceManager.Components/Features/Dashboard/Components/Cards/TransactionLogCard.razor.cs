using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Globalization;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards;

public partial class TransactionLogCard
{
    private bool _isLoading;
    private bool _hasError;
    private List<TransactionLogEntryDto> _data = [];
    private readonly RefreshVersionGate _refreshGate = new();

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public int Count { get; set; } = 10;

    [Inject] public required ILogger<TransactionLogCard> Logger { get; set; }
    [Inject] public required TransactionLogHttpClient TransactionLogHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required DashboardCardsSnapshotStore SnapshotStore { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        var version = _refreshGate.Claim();
        var user = await LoginService.GetLoggedUser();
        if (!_refreshGate.IsCurrent(version)) return;
        if (user is null)
        {
            _data = [];
            _isLoading = false;
            _hasError = false;
            return;
        }

        var result = await SnapshotStore.RefreshTransactionLogAsync(
            user.UserId,
            Count,
            _refreshGate,
            version,
            async () => await TransactionLogHttpClient.GetLastTransactions(user.UserId, Count),
            onSnapshotPainted: ShowData,
            onSnapshotMissing: ShowLoading,
            onRefreshed: ShowData);
        if (!_refreshGate.IsCurrent(version)) return;

        _isLoading = false;
        _hasError = result.IsBlockingFailure;
        if (_hasError)
        {
            Logger.LogError(result.Error, "Failed to load the transaction log.");
        }
        StateHasChanged();
    }

    private Task ShowData(List<TransactionLogEntryDto> data)
    {
        _data = data;
        _isLoading = false;
        _hasError = false;
        return InvokeAsync(StateHasChanged);
    }

    private Task ShowLoading()
    {
        _isLoading = true;
        _hasError = false;
        return InvokeAsync(StateHasChanged);
    }

    private static string GetAccountTypeIcon(AccountType accountType) => accountType switch
    {
        AccountType.Stock => Icons.Material.Filled.ShowChart,
        AccountType.Bond => Icons.Material.Filled.Savings,
        _ => Icons.Material.Filled.AccountBalanceWallet,
    };

    private static string FormatAmount(decimal value)
    {
        var formatted = value.ToString("N2", CultureInfo.InvariantCulture);
        return value > 0 ? $"+{formatted}" : formatted;
    }
}