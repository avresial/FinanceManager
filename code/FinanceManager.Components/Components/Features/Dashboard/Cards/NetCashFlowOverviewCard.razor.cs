using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Components.Features.Dashboard.Models;
using FinanceManager.Components.Models;
using FinanceManager.Components.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace FinanceManager.Components.Components.Features.Dashboard.Cards;

public partial class NetCashFlowOverviewCard
{
    private string _currency = "PLN";
    private decimal? _totalNetCashFlow;
    private List<TimeSeriesModel> _series = [];
    private bool _isLoading = true;
    private bool _hasError;

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    // When the dashboard supplies a prepared model the card renders it directly;
    // otherwise it self-loads from the cache service as in standalone usage.
    [Parameter] public TimeSeriesCardModel? Model { get; set; }

    [Inject] public required ILogger<NetCashFlowOverviewCard> Logger { get; set; }
    [Inject] public required DashboardOverviewCardsCacheService DashboardOverviewCardsCacheService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    // The hero shows the period total, so the caption frames it as a range sum rather
    // than a single month (which the hover state already covers).
    private string PeriodCaption
    {
        get
        {
            var start = StartDateTime.ToLocalTime().ToString("MMM yyyy", CultureInfo.InvariantCulture);
            var end = EndDateTime.ToLocalTime().ToString("MMM yyyy", CultureInfo.InvariantCulture);
            // Glyphs as code points so the source stays pure-ASCII (U+00B7 middle dot,
            // U+2013 en dash), matching the convention in TimeSeriesValueCard.
            var dot = (char)0x00B7;
            var dash = (char)0x2013;
            return start == end ? $"Total {dot} {start}" : $"Total {dot} {start} {dash} {end}";
        }
    }

    protected override Task OnParametersSetAsync() => Reload();

    private async Task Reload()
    {
        var currency = await SettingsService.GetCurrencyAsync();
        _currency = currency.ShortName;
        _totalNetCashFlow = null;

        if (Model is not null)
        {
            _series = [.. Model.Series.OrderBy(x => x.DateTime).Select(x => new TimeSeriesModel(x.DateTime, Math.Round(x.Value, 2)))];
            _totalNetCashFlow = Math.Round(_series.Sum(x => x.Value), 2);
            _hasError = false;
            _isLoading = false;
            return;
        }

        var user = await LoginService.GetLoggedUser();
        if (user is null)
        {
            _series = [];
            _hasError = false;
            _isLoading = false;
            return;
        }

        _isLoading = true;
        _hasError = false;
        StateHasChanged();

        try
        {
            var context = new DashboardOverviewCardsRefreshContext
            {
                UserId = user.UserId,
                CurrencyId = currency.Id,
                StartDateTime = StartDateTime,
                EndDateTime = EndDateTime,
            };

            var snapshot = await DashboardOverviewCardsCacheService.GetSnapshotAsync(context);
            _series = snapshot.NetCashFlowSeries
                .OrderBy(x => x.DateTime)
                .Select(x => new TimeSeriesModel(x.DateTime, Math.Round(x.Value, 2)))
                .ToList();
            _totalNetCashFlow = Math.Round(_series.Sum(x => x.Value), 2);
        }
        catch (Exception ex)
        {
            _series = [];
            _hasError = true;
            Logger.LogError(ex, "Error while getting net cash flow");
        }
        finally
        {
            _isLoading = false;
        }
    }
}