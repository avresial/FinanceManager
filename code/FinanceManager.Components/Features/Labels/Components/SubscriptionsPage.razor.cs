using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.Labels.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Commands;
using FinanceManager.Domain.Labels.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Features.Labels.Components;

public partial class SubscriptionsPage : ComponentBase
{
    private bool _isLoading = true;
    private bool _showInactive;
    private int? _userId;
    private Currency _currency = DefaultCurrency.PLN;
    private List<RecurringTransactionResult> _subscriptions = [];
    private readonly HashSet<Guid> _updating = [];

    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required RecurringTransactionDetectorHttpClient HttpClient { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required ILogger<SubscriptionsPage> Logger { get; set; }

    private List<RecurringTransactionResult> VisibleSubscriptions =>
        _subscriptions
            .Where(x => _showInactive || (!x.IsMuted && !x.IsCancelled))
            .OrderByDescending(x => x.IsFlaggedForReview)
            .ThenBy(x => x.NextExpectedChargeDate)
            .ToList();

    private IEnumerable<RecurringTransactionResult> ActiveSubscriptions =>
        _subscriptions.Where(x => !x.IsMuted && !x.IsCancelled);

    private int ActiveCount => ActiveSubscriptions.Count();
    private decimal ActiveMonthlyCost => ActiveSubscriptions.Sum(x => x.MonthlyCost);
    private int IncreaseCount => ActiveSubscriptions.Count(x => x.PriceDelta > 0);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null) return;

            _userId = user.UserId;
            _currency = await SettingsService.GetCurrencyAsync();
            _subscriptions = await HttpClient.GetRecurringTransactions(user.UserId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to load subscriptions");
            Snackbar.Add("Unable to load subscriptions.", Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private bool IsUpdating(RecurringTransactionResult item) => _updating.Contains(item.PatternId);

    private Task ToggleMuted(RecurringTransactionResult item) =>
        Update(item, !item.IsMuted, item.IsCancelled, item.IsFlaggedForReview);

    private Task ToggleCancelled(RecurringTransactionResult item) =>
        Update(item, item.IsMuted, !item.IsCancelled, item.IsFlaggedForReview);

    private Task ToggleFlag(RecurringTransactionResult item) =>
        Update(item, item.IsMuted, item.IsCancelled, !item.IsFlaggedForReview);

    private async Task Update(
        RecurringTransactionResult item,
        bool isMuted,
        bool isCancelled,
        bool isFlaggedForReview)
    {
        if (_userId is null || !_updating.Add(item.PatternId)) return;

        try
        {
            var saved = await HttpClient.Update(
                _userId.Value,
                item.PatternId,
                new UpdateRecurringSubscription(isMuted, isCancelled, isFlaggedForReview));
            if (!saved)
            {
                Snackbar.Add("Unable to update this subscription.", Severity.Error);
                return;
            }

            item.IsMuted = isMuted;
            item.IsCancelled = isCancelled;
            item.IsFlaggedForReview = isFlaggedForReview;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to update subscription {PatternId}", item.PatternId);
            Snackbar.Add("Unable to update this subscription.", Severity.Error);
        }
        finally
        {
            _updating.Remove(item.PatternId);
        }
    }

    private static string CadenceLabel(RecurringCadence cadence) =>
        cadence switch
        {
            RecurringCadence.Weekly => "Weekly",
            RecurringCadence.Monthly => "Monthly",
            RecurringCadence.Quarterly => "Quarterly",
            RecurringCadence.Annual => "Annual",
            _ => cadence.ToString()
        };
}