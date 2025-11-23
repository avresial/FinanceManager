using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace FinanceManager.Application.Services;

internal class CurrencyExchangeService(HttpClient httpClient, ILogger<CurrencyExchangeService> logger) : ICurrencyExchangeService
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
        }


        // TODO implement fallback logic to get exchange rate from another source
        return default;
    }

    public async Task<decimal> GetPricePerUnit(StockPrice stockPrice, Currency currency, DateTime date)
    {
        if (stockPrice is null) return 1;

        var priceInRightCurrency = await GetExchangeRateAsync(stockPrice.Currency, currency, date.Date);
        if (priceInRightCurrency is not null)
            return stockPrice.PricePerUnit * priceInRightCurrency.Value;

        return 1;
    }
}