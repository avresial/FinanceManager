using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Enums;
using System.Globalization;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class AssetsHttpClient(HttpClient httpClient)
{
    public async Task<bool> IsAnyAccountWithAssets(int userId)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<bool>($"{httpClient.BaseAddress}api/Assets/IsAnyAccountWithAssets/{userId}");
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end)
    {
        var result = await httpClient.GetFromJsonAsync<List<TimeSeriesModel>>($"{httpClient.BaseAddress}api/Assets/GetAssetsTimeSeries/{userId}/{currency.Id}/{start:O}/{end:O}");
        return result ?? [];
    }

    public async Task<List<TimeSeriesModel>> GetAssetsTimeSeries(int userId, Currency currency, DateTime start, DateTime end, InvestmentType investmentType)
    {
        var result = await httpClient.GetFromJsonAsync<List<TimeSeriesModel>>($"{httpClient.BaseAddress}api/Assets/GetAssetsTimeSeries/{userId}/{currency.Id}/{start:O}/{end:O}/{investmentType}");
        return result ?? [];
    }

    public async Task<List<NameValueResult>> GetEndAssetsPerAccount(int userId, Currency currency, DateTime asOfDate)
    {
        var result = await httpClient.GetFromJsonAsync<List<NameValueResult>>($"{httpClient.BaseAddress}api/Assets/GetEndAssetsPerAccount/{userId}/{currency.Id}/{asOfDate:O}");
        return result ?? [];
    }

    public async Task<List<NameValueResult>> GetEndAssetsPerType(int userId, Currency currency, DateTime asOfDate)
    {
        var result = await httpClient.GetFromJsonAsync<List<NameValueResult>>($"{httpClient.BaseAddress}api/Assets/GetEndAssetsPerType/{userId}/{currency.Id}/{asOfDate:O}");
        return result ?? [];
    }

    public async Task<InvestmentPaycheckEstimate> GetInvestmentPaycheckEstimate(int userId, Currency currency, DateTime asOfDate, decimal withdrawalRate = 0.05m, int salaryMonths = 3)
    {
        string endpoint = $"{httpClient.BaseAddress}api/Assets/GetInvestmentPaycheckEstimate/{userId}/{currency.Id}/{asOfDate:O}?withdrawalRate={withdrawalRate.ToString(CultureInfo.InvariantCulture)}&salaryMonths={salaryMonths}";
        var result = await httpClient.GetFromJsonAsync<InvestmentPaycheckEstimate>(endpoint);
        return result ?? new InvestmentPaycheckEstimate
        {
            AsOfDate = asOfDate,
            AnnualWithdrawalRate = withdrawalRate,
            SalaryMonthsRequested = salaryMonths,
        };
    }
}