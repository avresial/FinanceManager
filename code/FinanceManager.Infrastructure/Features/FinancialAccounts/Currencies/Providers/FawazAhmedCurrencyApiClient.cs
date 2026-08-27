using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Shared.Services;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Providers;

internal sealed class FawazAhmedCurrencyApiClient(
    HttpClient httpClient,
    ILogger<FawazAhmedCurrencyApiClient> logger,
    IDateTimeProvider dateTimeProvider) : ICurrencyExchangeRateProvider
{
    // The npm-backed API has no date-versioned releases before its migration on 2024-03-02.
    private static readonly DateOnly _firstAvailableDate = new(2024, 3, 2);

    public async Task<CurrencyExchangeRateProviderResult> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date)
    {
        if (DateOnly.FromDateTime(date) < _firstAvailableDate)
            return new(CurrencyExchangeRateProviderStatus.OutOfRange);

        try
        {
            using var response = await httpClient.GetAsync($"https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@{date:yyyy-MM-dd}/v1/currencies/{fromCurrency.ShortName.ToLowerInvariant()}.json");
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.NotFound && DateOnly.FromDateTime(date) == DateOnly.FromDateTime(dateTimeProvider.UtcNow))
                {
                    var retryAtUtc = DateTime.SpecifyKind(date.Date.AddDays(1), DateTimeKind.Utc);
                    logger.LogInformation(
                        "Currency API has not published the exchange rate for {FromCurrency} to {ToCurrency} on {Date:yyyy-MM-dd}; it is safe to retry after {RetryAtUtc:O}.",
                        fromCurrency,
                        toCurrency,
                        date,
                        retryAtUtc);
                    return new(CurrencyExchangeRateProviderStatus.NotYetPublished, RetryAtUtc: retryAtUtc);
                }

                var errorMessage = await response.Content.ReadAsStringAsync();
                logger.LogWarning("Currency API returned {StatusCode} for {FromCurrency} to {ToCurrency} on {Date}: {Message}", response.StatusCode, fromCurrency, toCurrency, date, errorMessage.Trim());
                return new(response.StatusCode == HttpStatusCode.NotFound
                    ? CurrencyExchangeRateProviderStatus.NotFound
                    : CurrencyExchangeRateProviderStatus.Failed);
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(contentStream);

            if (document.RootElement.TryGetProperty(fromCurrency.ShortName.ToLowerInvariant(), out var fromCurrencyRates) &&
                fromCurrencyRates.TryGetProperty(toCurrency.ShortName.ToLowerInvariant(), out var exchangeRate) &&
                exchangeRate.TryGetDecimal(out var value))
                return new(CurrencyExchangeRateProviderStatus.Success, value);

            return new(CurrencyExchangeRateProviderStatus.NotFound);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Currency API returned invalid JSON for {FromCurrency} to {ToCurrency} on {Date}", fromCurrency, toCurrency, date);
            return new(CurrencyExchangeRateProviderStatus.Failed);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogDebug(ex, "Fawaz request cancelled or timed out for {FromCurrency} to {ToCurrency} on {Date}.", fromCurrency, toCurrency, date);
            return new(CurrencyExchangeRateProviderStatus.Failed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get exchange rate from {FromCurrency} to {ToCurrency} on {Date}", fromCurrency, toCurrency, date);
            return new(CurrencyExchangeRateProviderStatus.Failed);
        }
    }

    public async Task<List<(DateTime Date, CurrencyExchangeRateProviderResult Result)>> GetExchangeRateAsync(
        Currency fromCurrency, Currency toCurrency, DateTime dateStart, DateTime dateEnd)
    {
        var start = dateStart.Date;
        var end = dateEnd.Date;
        var totalDays = (end - start).Days + 1;
        if (totalDays <= 0) return [];

        const int batchSize = 50;
        List<(DateTime Date, CurrencyExchangeRateProviderResult Result)> rates = [];

        for (var offset = 0; offset < totalDays; offset += batchSize)
        {
            var currentBatchSize = Math.Min(batchSize, totalDays - offset);
            List<DateTime> batchDates = [];
            List<Task<CurrencyExchangeRateProviderResult>> batchTasks = [];

            for (var i = 0; i < currentBatchSize; i++)
            {
                var date = start.AddDays(offset + i);
                batchDates.Add(date);
                batchTasks.Add(DateOnly.FromDateTime(date) < _firstAvailableDate
                    ? Task.FromResult(new CurrencyExchangeRateProviderResult(CurrencyExchangeRateProviderStatus.OutOfRange))
                    : GetExchangeRateAsync(fromCurrency, toCurrency, date));
            }

            var batchResults = await Task.WhenAll(batchTasks);
            for (var i = 0; i < batchResults.Length; i++)
                rates.Add((batchDates[i], batchResults[i]));
        }

        return rates;
    }
}