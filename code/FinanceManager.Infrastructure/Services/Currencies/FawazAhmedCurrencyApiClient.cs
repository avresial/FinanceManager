using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace FinanceManager.Infrastructure.Services.Currencies;

internal sealed class FawazAhmedCurrencyApiClient(
    HttpClient httpClient,
    ILogger<FawazAhmedCurrencyApiClient> logger) : ICurrencyExchangeRateProvider
{
    public async Task<decimal?> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date)
    {
        try
        {
            var response = await httpClient.GetAsync($"https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@{date:yyyy-MM-dd}/v1/currencies/{fromCurrency.ShortName.ToLower()}.json");
            var jObject = JObject.Parse(await response.Content.ReadAsStringAsync());
            var tokenPath = $"$.{fromCurrency.ShortName.ToLower()}.{toCurrency.ShortName.ToLower()}";

            return (decimal?)jObject.SelectToken(tokenPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get exchange rate from {FromCurrency} to {ToCurrency} on {Date}", fromCurrency, toCurrency, date);
            return null;
        }
    }

    public async Task<List<(DateTime Date, decimal? Value)>> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime dateStart, DateTime dateEnd)
    {
        var start = dateStart.Date;
        var end = dateEnd.Date;
        var totalDays = (end - start).Days + 1;
        if (totalDays <= 0) return [];

        const int batchSize = 50;
        List<(DateTime Date, decimal? Value)> rates = [];

        for (var offset = 0; offset < totalDays; offset += batchSize)
        {
            var currentBatchSize = Math.Min(batchSize, totalDays - offset);
            List<DateTime> batchDates = [];
            List<Task<decimal?>> batchTasks = [];

            for (var i = 0; i < currentBatchSize; i++)
            {
                var date = start.AddDays(offset + i);
                batchDates.Add(date);
                batchTasks.Add(GetExchangeRateAsync(fromCurrency, toCurrency, date));
            }

            var batchResults = await Task.WhenAll(batchTasks);
            for (var i = 0; i < batchResults.Length; i++)
                rates.Add((batchDates[i], batchResults[i]));
        }

        return rates;
    }
}