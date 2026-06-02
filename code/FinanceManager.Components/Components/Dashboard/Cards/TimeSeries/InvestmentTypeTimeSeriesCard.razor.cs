// using FinanceManager.Components.HttpClients;
// using FinanceManager.Domain.Entities.Currencies;
// using FinanceManager.Domain.Entities.MoneyFlowModels;
// using FinanceManager.Domain.Services;
// using Microsoft.AspNetCore.Components;
// using Microsoft.Extensions.Logging;

// namespace FinanceManager.Components.Components.Dashboard.Cards.TimeSeries;

// public partial class InvestmentTypeTimeSeriesCard
// {
//     private bool _isLoading;
//     private bool _hasError;
//     private string _currency = "PLN";
//     private List<TimeSeriesModel> _priceTimeseries = [];

//     [Parameter] public DateTime StartDateTime { get; set; }
//     [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
//     [Parameter] public string Height { get; set; } = "250px";

//     [Inject] public required ISettingsService SettingsService { get; set; }
//     [Inject] public required ILoginService LoginService { get; set; }
//     [Inject] public required ILogger<InvestmentTypeTimeSeriesCard> Logger { get; set; }
//     [Inject] public required AssetsHttpClient AssetsHttpClient { get; set; }

//     protected override Task OnParametersSetAsync() => Reload();

//     private async Task Reload()
//     {
//         var user = await LoginService.GetLoggedUser();
//         if (user is null) return;

//         _isLoading = true;
//         _hasError = false;
//         StateHasChanged();
//         try
//         {
//             _currency = SettingsService.GetCurrency().ShortName;

//             _priceTimeseries.Clear();
//             var data = await AssetsHttpClient.GetAssetsTimeSeries(user.UserId, DefaultCurrency.PLN, StartDateTime, EndDateTime);
//             _priceTimeseries.AddRange(data.OrderBy(x => x.DateTime));
//         }
//         catch (Exception ex)
//         {
//             _hasError = true;
//             Logger.LogError(ex, "Error getting investment type time series data");
//         }
//         finally
//         {
//             _isLoading = false;
//         }
//     }
// }