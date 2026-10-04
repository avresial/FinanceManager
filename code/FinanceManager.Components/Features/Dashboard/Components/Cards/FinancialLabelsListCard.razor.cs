using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards;

public partial class FinancialLabelsListCard
{
    private readonly RefreshVersionGate _gate = new();
    private bool _isLoading;
    private bool _hasError;
    private Currency _currency = DefaultCurrency.PLN;
    private List<NameValueResult> _data = [];

    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required MoneyFlowHttpClient MoneyFlowHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISnapshotRefreshCoordinator Coordinator { get; set; }

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Parameter] public CardMode CardMode { get; set; } = CardMode.List;

    // When the dashboard supplies a prepared model the card renders it directly;
    // otherwise it self-loads from the API as in standalone usage.
    [Parameter] public NameValueListCardModel? Model { get; set; }

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
            _data = NonZero(model.Items);
            _hasError = false;
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
                _hasError = false;
                return;
            }

            var currency = await SettingsService.GetCurrencyAsync();
            if (!_gate.IsCurrent(version)) return;
            _currency = currency;
            _hasError = false;
            var result = await Coordinator.RunAsync(new SnapshotRefreshRequest<FinancialLabelsSnapshot, NameValueListCardModel>
            {
                Key = $"financial-labels:{user.UserId}:{currency.Id}",
                Gate = _gate,
                ClaimedVersion = version,
                // The range is metadata: never show old amounts under a different range or currency.
                ToModel = snapshot => snapshot.UserId == user.UserId && snapshot.CurrencyId == currency.Id
                    && snapshot.StartDate == start && snapshot.EndDate.Date == end.Date
                    ? new NameValueListCardModel(NonZero(snapshot.Items)) : null,
                FetchAsync = async () =>
                {
                    var labels = await MoneyFlowHttpClient.GetLabelsValue(user.UserId, start, end);
                    return new NameValueListCardModel(NonZero(labels));
                },
                ToSnapshot = model => new FinancialLabelsSnapshot
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
            _hasError = result.IsBlockingFailure;
            _isLoading = false;
        }
        catch
        {
            if (!_gate.IsCurrent(version)) return;
            _data = [];
            _hasError = true;
            _isLoading = false;
        }
    }

    private Task ShowData(NameValueListCardModel model)
    {
        _data = [.. model.Items];
        _isLoading = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private static List<NameValueResult> NonZero(IEnumerable<NameValueResult> items) => [.. items.Where(x => x.Value != 0)];
}