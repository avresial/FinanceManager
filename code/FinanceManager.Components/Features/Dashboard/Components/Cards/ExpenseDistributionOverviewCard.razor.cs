using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards;

public partial class ExpenseDistributionOverviewCard
{
    private readonly RefreshVersionGate _gate = new();
    private bool _isLoading = true;
    private Currency _currency = DefaultCurrency.PLN;
    private List<NameValueResult> _data = [];

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    // When the dashboard supplies a prepared model the card renders it directly;
    // otherwise it self-loads from the API as in standalone usage.
    [Parameter] public NameValueListCardModel? Model { get; set; }

    [Inject] public required ILogger<ExpenseDistributionOverviewCard> Logger { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required MoneyFlowHttpClient MoneyFlowHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISnapshotRefreshCoordinator Coordinator { get; set; }

    protected override Task OnParametersSetAsync() => LoadData();

    private async Task LoadData()
    {
        // Claimed first on both paths so a parent-supplied model supersedes any in-flight self-load.
        var version = _gate.Claim();
        if (Model is not null)
        {
            var model = Model;
            var supplied = await SettingsService.GetCurrencyAsync();
            if (!_gate.IsCurrent(version)) return;
            _currency = supplied;
            _data = [.. model.Items];
            _isLoading = false;
            return;
        }

        var start = StartDateTime.Date;
        var end = EndDateTime;
        try
        {
            var user = await LoginService.GetLoggedUser();
            if (!_gate.IsCurrent(version)) return;
            if (user is null)
            {
                _data = [];
                _isLoading = false;
                return;
            }

            var currency = await SettingsService.GetCurrencyAsync();
            if (!_gate.IsCurrent(version)) return;
            _currency = currency;
            var result = await Coordinator.RunAsync(new SnapshotRefreshRequest<ExpenseDistributionSnapshot, NameValueListCardModel>
            {
                Key = $"expense-distribution:{user.UserId}:{currency.Id}",
                Gate = _gate,
                ClaimedVersion = version,
                // The range is metadata: never show old amounts under a different range or currency.
                ToModel = snapshot => snapshot.UserId == user.UserId && snapshot.CurrencyId == currency.Id
                    && snapshot.StartDate == start && snapshot.EndDate.Date == end.Date
                    ? new NameValueListCardModel([.. snapshot.Items]) : null,
                FetchAsync = async () =>
                {
                    var items = await MoneyFlowHttpClient.GetExpenseDistribution(user.UserId, currency, start, end);
                    return new NameValueListCardModel([.. items]);
                },
                ToSnapshot = model => new ExpenseDistributionSnapshot
                {
                    UserId = user.UserId,
                    CurrencyId = currency.Id,
                    StartDate = start,
                    EndDate = end,
                    Items = [.. model.Items],
                },
                OnSnapshotPainted = ShowData,
                OnSnapshotMissing = () =>
                {
                    _data = [];
                    _isLoading = true;
                    StateHasChanged();
                    return Task.CompletedTask;
                },
                OnRefreshed = ShowData,
            });
            if (!_gate.IsCurrent(version)) return;
            _isLoading = false;
            if (result.IsBlockingFailure)
                ReportLoadFailure(result.Error);
            else if (result.Outcome == SnapshotRefreshOutcome.Failed)
                Logger.LogWarning(result.Error, "Expense distribution refresh failed; keeping the last distribution.");
        }
        catch (Exception ex)
        {
            if (!_gate.IsCurrent(version)) return;
            _data = [];
            _isLoading = false;
            ReportLoadFailure(ex);
        }
    }

    private Task ShowData(NameValueListCardModel model)
    {
        _data = [.. model.Items];
        _isLoading = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private void ReportLoadFailure(Exception? ex)
    {
        Logger.LogError(ex, "Unable to load expense distribution.");
        Snackbar.Add("Unable to load expense distribution.", Severity.Error);
    }
}