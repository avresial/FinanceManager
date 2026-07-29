using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.Liabilities;

public partial class LiabilitiesDistributionOverviewCard
{
    private bool _isLoading;
    private Currency _currency = DefaultCurrency.PLN;
    private List<NameValueResult> _typeData = [];
    private List<NameValueResult> _accountData = [];

    // Guards against a slower, earlier self-load overwriting a newer range/currency selection.
    private readonly RefreshVersionGate _gate = new();

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    // When the dashboard supplies a prepared model the card renders it directly;
    // otherwise it self-loads from the API as in standalone usage.
    [Parameter] public DistributionCardModel? Model { get; set; }

    [Inject] public required ILogger<LiabilitiesDistributionOverviewCard> Logger { get; set; }
    [Inject] public required LiabilitiesHttpClient LiabilitiesHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required LiabilitiesSnapshotStore SnapshotStore { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _currency = await SettingsService.GetCurrencyAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        // The dashboard-supplied model is already backed by the dashboard overview snapshot, so it
        // renders directly and this card only snapshots its standalone self-loading path.
        if (Model is not null)
        {
            _typeData = ToPositive(Model.TypeData);
            _accountData = ToPositive(Model.AccountData);
            _isLoading = false;
            StateHasChanged();
            return;
        }

        await LoadSelf();
    }

    // Stale-while-revalidate self-load: paint the stored breakdown, always re-fetch, and only repaint
    // and re-persist when the fresh breakdown differs. See docs/codebase/UI-SNAPSHOTS.md.
    private async Task LoadSelf()
    {
        var requestVersion = _gate.Claim();
        var startDate = StartDateTime;
        var endDate = EndDateTime;

        if (startDate == new DateTime())
        {
            CommitEmpty(requestVersion);
            return;
        }

        var user = await LoginService.GetLoggedUser();
        if (user is null)
        {
            CommitEmpty(requestVersion);
            return;
        }

        var result = await SnapshotStore.RefreshDistributionAsync(
            user.UserId,
            _currency.Id,
            startDate,
            endDate,
            _gate,
            fetchAsync: async () =>
            {
                var typeTask = LiabilitiesHttpClient.GetEndLiabilitiesPerType(user.UserId, startDate, endDate).ToListAsync().AsTask();
                var accountTask = LiabilitiesHttpClient.GetEndLiabilitiesPerAccount(user.UserId, startDate, endDate).ToListAsync().AsTask();
                await Task.WhenAll(typeTask, accountTask);
                return new DistributionCardModel(ToPositive(await typeTask), ToPositive(await accountTask));
            },
            onSnapshotPainted: ShowData,
            onSnapshotMissing: () =>
            {
                _isLoading = true;
                StateHasChanged();
                return Task.CompletedTask;
            },
            onRefreshed: ShowData);

        if (!_gate.IsCurrent(requestVersion))
            return;

        // This card has no blocking error surface; a failed refresh behind a painted snapshot keeps it
        // visible, and a failed first load simply leaves the empty state (matching the previous behaviour).
        if (result.IsBlockingFailure)
            Logger.LogError(result.Error, "Error loading liabilities distribution");

        _isLoading = false;
        StateHasChanged();
    }

    private Task ShowData(DistributionCardModel model)
    {
        _typeData = model.TypeData;
        _accountData = model.AccountData;
        _isLoading = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private void CommitEmpty(int requestVersion)
    {
        if (!_gate.IsCurrent(requestVersion)) return;
        _typeData = [];
        _accountData = [];
        _isLoading = false;
        StateHasChanged();
    }

    // Liabilities are returned as negative magnitudes; flip them so the pie and legend show positive amounts.
    private static List<NameValueResult> ToPositive(IEnumerable<NameValueResult> data) =>
        [.. data.Select(x => new NameValueResult { Name = x.Name, Value = Math.Abs(x.Value) })];
}