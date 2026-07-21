using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Globalization;

namespace FinanceManager.Components.Components.Features.Dashboard.Cards;

public partial class TransactionLogCard
{
    private bool _isLoading;
    private bool _hasError;
    private List<TransactionLogEntryDto> _data = [];

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public int Count { get; set; } = 10;

    [Inject] public required ILogger<TransactionLogCard> Logger { get; set; }
    [Inject] public required TransactionLogHttpClient TransactionLogHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;
        _hasError = false;
        StateHasChanged();

        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null)
            {
                _data = [];
                return;
            }

            _data = await TransactionLogHttpClient.GetLastTransactions(user.UserId, Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load the transaction log.");
            _hasError = true;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
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