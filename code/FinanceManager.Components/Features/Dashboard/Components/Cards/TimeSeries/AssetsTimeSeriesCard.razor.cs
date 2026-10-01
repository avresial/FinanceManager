using FinanceManager.Components.Features.Dashboard.Models;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.TimeSeries;

public partial class AssetsTimeSeriesCard
{
    [Parameter] public TimeSeriesCardModel? Model { get; set; }
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public bool HasError { get; set; }
    [Parameter] public EventCallback OnRetry { get; set; }
    [Parameter] public string CurrencyShortName { get; set; } = "PLN";
    [Parameter] public string Height { get; set; } = "250px";
}