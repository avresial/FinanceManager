using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Providers;

/// <summary>
/// Resolves official PLN-based exchange rates from the public, keyless NBP (Narodowy Bank Polski) API,
/// table A. NBP quotes a mid rate as PLN per one unit of the foreign currency, so this provider serves
/// any pair where exactly one side is PLN, computing the inverse when the request is quoted PLN→X. Pairs
/// without a PLN leg return <see cref="CurrencyExchangeRateProviderStatus.NotFound"/> so the resolution
/// chain falls through to another provider. Non-publication days (weekends, Polish bank holidays) come
/// back from NBP as HTTP 404 and are reported as NotFound rather than a failure.
/// </summary>
internal sealed class NbpCurrencyExchangeRateProvider(
    HttpClient httpClient,
    IOptions<NbpOptions> options,
    ILogger<NbpCurrencyExchangeRateProvider> logger) : ICurrencyExchangeRateProvider
{
    private const string _pln = "PLN";

    // NBP caps a single table-A range request at 367 days; stay well under it.
    private const int _rangeChunkDays = 180;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<CurrencyExchangeRateProviderResult> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date)
    {
        if (!options.Value.Enabled)
            return new(CurrencyExchangeRateProviderStatus.NotFound);

        if (IsPln(fromCurrency) && IsPln(toCurrency))
            return new(CurrencyExchangeRateProviderStatus.Success, 1m);

        if (!TryResolveForeignCode(fromCurrency, toCurrency, out var foreignCode, out var invert))
            return new(CurrencyExchangeRateProviderStatus.NotFound);

        var url = BuildUrl($"exchangerates/rates/a/{foreignCode}/{Iso(date)}/?format=json");
        var (failed, rates) = await FetchSeriesAsync(url, foreignCode, date, date);
        if (failed)
            return new(CurrencyExchangeRateProviderStatus.Failed);

        if (!rates.TryGetValue(date.Date, out var mid) || mid <= 0)
            return new(CurrencyExchangeRateProviderStatus.NotFound);

        // NBP mid = PLN per 1 foreign unit. For CODE→PLN the rate is the mid directly; for PLN→CODE it
        // is the inverse.
        return new(CurrencyExchangeRateProviderStatus.Success, invert ? 1m / mid : mid);
    }

    public async Task<List<(DateTime Date, CurrencyExchangeRateProviderResult Result)>> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime dateStart, DateTime dateEnd)
    {
        var start = dateStart.Date;
        var end = dateEnd.Date;
        if (start > end) (start, end) = (end, start);
        if ((end - start).Days < 0) return [];

        // Match the single-date method: a disabled provider fully bypasses, even for PLN→PLN.
        if (!options.Value.Enabled)
            return EnumerateDates(start, end).Select(d => (d, new CurrencyExchangeRateProviderResult(CurrencyExchangeRateProviderStatus.NotFound))).ToList();

        if (!TryResolveForeignCode(fromCurrency, toCurrency, out var foreignCode, out var invert))
        {
            if (IsPln(fromCurrency) && IsPln(toCurrency))
                return EnumerateDates(start, end).Select(d => (d, new CurrencyExchangeRateProviderResult(CurrencyExchangeRateProviderStatus.Success, 1m))).ToList();

            return EnumerateDates(start, end).Select(d => (d, new CurrencyExchangeRateProviderResult(CurrencyExchangeRateProviderStatus.NotFound))).ToList();
        }

        Dictionary<DateTime, decimal> merged = [];
        HashSet<DateTime> failedDates = [];
        for (var chunkStart = start; chunkStart <= end; chunkStart = chunkStart.AddDays(_rangeChunkDays))
        {
            var chunkEnd = chunkStart.AddDays(_rangeChunkDays - 1);
            if (chunkEnd > end) chunkEnd = end;

            var url = BuildUrl($"exchangerates/rates/a/{foreignCode}/{Iso(chunkStart)}/{Iso(chunkEnd)}/?format=json");
            var (failed, rates) = await FetchSeriesAsync(url, foreignCode, chunkStart, chunkEnd);
            if (failed)
            {
                failedDates.UnionWith(EnumerateDates(chunkStart, chunkEnd));
                continue;
            }

            foreach (var (day, mid) in rates)
                merged[day] = mid;
        }

        List<(DateTime Date, CurrencyExchangeRateProviderResult Result)> results = [];
        foreach (var day in EnumerateDates(start, end))
        {
            if (failedDates.Contains(day))
                results.Add((day, new(CurrencyExchangeRateProviderStatus.Failed)));
            else if (merged.TryGetValue(day, out var mid) && mid > 0)
                results.Add((day, new(CurrencyExchangeRateProviderStatus.Success, invert ? 1m / mid : mid)));
            else
                results.Add((day, new(CurrencyExchangeRateProviderStatus.NotFound)));
        }

        return results;
    }

    // Returns (Failed, effectiveDate→mid). Failed marks a transport/parse error so the caller can report
    // it distinctly; a 404 (non-publication day or unknown currency) is not a failure and yields an empty
    // map so the date is simply reported as NotFound.
    private async Task<(bool Failed, Dictionary<DateTime, decimal> Rates)> FetchSeriesAsync(
        string url, string code, DateTime start, DateTime end)
    {
        try
        {
            using var response = await httpClient.GetAsync(url);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return (false, []);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("NBP returned {StatusCode} for {Code} {Start:yyyy-MM-dd}..{End:yyyy-MM-dd}.", response.StatusCode, code, start, end);
                return (true, []);
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            var payload = await JsonSerializer.DeserializeAsync<NbpRatesResponse>(stream, _jsonOptions);
            if (payload?.Rates is null || payload.Rates.Count == 0)
                return (false, []);

            Dictionary<DateTime, decimal> rates = [];
            foreach (var rate in payload.Rates)
            {
                if (DateTime.TryParseExact(rate.EffectiveDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) && rate.Mid > 0)
                    rates[day.Date] = rate.Mid;
            }

            return (false, rates);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogDebug(ex, "NBP request cancelled or timed out for {Code} {Start:yyyy-MM-dd}..{End:yyyy-MM-dd}.", code, start, end);
            return (true, []);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "NBP returned invalid JSON for {Code} {Start:yyyy-MM-dd}..{End:yyyy-MM-dd}.", code, start, end);
            return (true, []);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NBP request failed for {Code} {Start:yyyy-MM-dd}..{End:yyyy-MM-dd}.", code, start, end);
            return (true, []);
        }
    }

    // Exactly one side must be PLN for a direct NBP lookup. `invert` is true for PLN→CODE (the mid must be
    // inverted), false for CODE→PLN.
    private static bool TryResolveForeignCode(Currency fromCurrency, Currency toCurrency, out string foreignCode, out bool invert)
    {
        if (IsPln(fromCurrency) && !IsPln(toCurrency))
        {
            foreignCode = Normalize(toCurrency);
            invert = true;
            return true;
        }

        if (IsPln(toCurrency) && !IsPln(fromCurrency))
        {
            foreignCode = Normalize(fromCurrency);
            invert = false;
            return true;
        }

        foreignCode = string.Empty;
        invert = false;
        return false;
    }

    private string BuildUrl(string relativePath)
    {
        var baseUrl = options.Value.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/{relativePath}";
    }

    private static IEnumerable<DateTime> EnumerateDates(DateTime start, DateTime end)
    {
        for (var day = start; day <= end; day = day.AddDays(1))
            yield return day;
    }

    private static bool IsPln(Currency currency) => string.Equals(currency.ShortName, _pln, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(Currency currency) => currency.ShortName.Trim().ToUpperInvariant();

    // yyyy-MM-dd is numeric; force invariant culture so non-Latin digit cultures still emit ASCII dates.
    private static string Iso(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed record NbpRatesResponse(
        [property: JsonPropertyName("rates")] List<NbpRate>? Rates);

    private sealed record NbpRate(
        [property: JsonPropertyName("effectiveDate")] string EffectiveDate,
        [property: JsonPropertyName("mid")] decimal Mid);
}