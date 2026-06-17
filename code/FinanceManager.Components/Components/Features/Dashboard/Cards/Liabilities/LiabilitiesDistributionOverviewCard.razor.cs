using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Components.Features.Dashboard.Models;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Features.Dashboard.Cards.Liabilities;

public partial class LiabilitiesDistributionOverviewCard
{
    private bool _isLoading;
    private Currency _currency = DefaultCurrency.PLN;
    private List<NameValueResult> _typeData = [];
    private List<NameValueResult> _accountData = [];

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

    protected override void OnInitialized()
    {
        _currency = SettingsService.GetCurrency();
    }

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            if (Model is not null)
            {
                _typeData = ToPositive(Model.TypeData);
                _accountData = ToPositive(Model.AccountData);
                return;
            }

            if (StartDateTime == new DateTime())
            {
                _typeData = [];
                _accountData = [];
                return;
            }

            var user = await LoginService.GetLoggedUser();
            if (user is null)
            {
                _typeData = [];
                _accountData = [];
                return;
            }

            var typeTask = LiabilitiesHttpClient.GetEndLiabilitiesPerType(user.UserId, StartDateTime, EndDateTime).ToListAsync().AsTask();
            var accountTask = LiabilitiesHttpClient.GetEndLiabilitiesPerAccount(user.UserId, StartDateTime, EndDateTime).ToListAsync().AsTask();
            await Task.WhenAll(typeTask, accountTask);

            _typeData = ToPositive(await typeTask);
            _accountData = ToPositive(await accountTask);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading liabilities distribution");
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    // Liabilities are returned as negative magnitudes; flip them so the pie and legend show positive amounts.
    private static List<NameValueResult> ToPositive(IEnumerable<NameValueResult> data) =>
        [.. data.Select(x => new NameValueResult { Name = x.Name, Value = Math.Abs(x.Value) })];
}