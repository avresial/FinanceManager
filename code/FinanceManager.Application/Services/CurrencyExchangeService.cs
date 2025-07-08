using FinanceManager.Domain.Services;
using Newtonsoft.Json.Linq;

namespace FinanceManager.Application.Services;
internal class CurrencyExchangeService() : ICurrencyExchangeService
{
    public async Task<decimal?> GetExchangeRateAsync(string fromCurrency, string toCurrency, DateTime date)
    {
        HttpClient httpClient = new(); // TODO change to IHttpClientFactory

        try
        {
            var response = await httpClient.GetAsync($"https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/{fromCurrency.ToLower()}.json");
            var jObject = JObject.Parse(await response.Content.ReadAsStringAsync());

            var tokenPath = $"$.{fromCurrency.ToLower()}.{toCurrency.ToLower()}";

            return (decimal)jObject.SelectToken(tokenPath);
        }
        catch (Exception ex)
        {

        }

        return default;
    }
}
