using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Providers;

/// <summary>
/// Resolves official EUR reference rates from the public, keyless ECB (European Central Bank) Data Portal
/// EXR dataset. Each series (<c>D.{CURRENCY}.EUR.SP00.A</c>) quotes the observation as units of the
/// foreign currency per one EUR, so this provider serves EUR pairs in both directions and computes cross
/// rates for two non-EUR currencies that are both quoted against EUR (<c>rate(A→B) = obs(B)/obs(A)</c>).
/// Unsupported currencies and non-publication days come back from ECB as HTTP 404 and are reported as
/// <see cref="CurrencyExchangeRateProviderStatus.NotFound"/> rather than a failure.
/// </summary>
internal sealed class EcbCurrencyExchangeRateProvider(
    HttpClient httpClient,
    IOptions<EcbOptions> options,
    ILogger<EcbCurrencyExchangeRateProvider> logger) : ICurrencyExchangeRateProvider
{
    private const string _eur = "EUR";

    public async Task<CurrencyExchangeRateProviderResult> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime date)
    {
        if (!options.Value.Enabled)
            return new(CurrencyExchangeRateProviderStatus.NotFound);

        var fromEur = IsEur(fromCurrency);
        var toEur = IsEur(toCurrency);

        if (fromEur && toEur)
            return new(CurrencyExchangeRateProviderStatus.Success, 1m);

        if (fromEur)
        {
            // EUR → CODE: the observation is already CODE per EUR.
            var (failed, obs) = await GetObservationAsync(Normalize(toCurrency), date);
            if (failed) return new(CurrencyExchangeRateProviderStatus.Failed);
            return obs is decimal codePerEur && codePerEur > 0
                ? new(CurrencyExchangeRateProviderStatus.Success, codePerEur)
                : new(CurrencyExchangeRateProviderStatus.NotFound);
        }

        if (toEur)
        {
            // CODE → EUR: invert CODE per EUR.
            var (failed, obs) = await GetObservationAsync(Normalize(fromCurrency), date);
            if (failed) return new(CurrencyExchangeRateProviderStatus.Failed);
            return obs is decimal codePerEur && codePerEur > 0
                ? new(CurrencyExchangeRateProviderStatus.Success, 1m / codePerEur)
                : new(CurrencyExchangeRateProviderStatus.NotFound);
        }

        // Cross rate A → B via EUR: (B per EUR) / (A per EUR).
        var (fromFailed, fromObs) = await GetObservationAsync(Normalize(fromCurrency), date);
        if (fromFailed) return new(CurrencyExchangeRateProviderStatus.Failed);
        if (fromObs is not decimal aPerEur || aPerEur <= 0)
            return new(CurrencyExchangeRateProviderStatus.NotFound);

        var (toFailed, toObs) = await GetObservationAsync(Normalize(toCurrency), date);
        if (toFailed) return new(CurrencyExchangeRateProviderStatus.Failed);
        if (toObs is not decimal bPerEur || bPerEur <= 0)
            return new(CurrencyExchangeRateProviderStatus.NotFound);

        return new(CurrencyExchangeRateProviderStatus.Success, bPerEur / aPerEur);
    }

    public async Task<List<(DateTime Date, CurrencyExchangeRateProviderResult Result)>> GetExchangeRateAsync(Currency fromCurrency, Currency toCurrency, DateTime dateStart, DateTime dateEnd)
    {
        var start = dateStart.Date;
        var end = dateEnd.Date;
        if (start > end) (start, end) = (end, start);
        if ((end - start).Days < 0) return [];

        var fromEur = IsEur(fromCurrency);
        var toEur = IsEur(toCurrency);

        if (!options.Value.Enabled)
            return EnumerateDates(start, end).Select(d => (d, new CurrencyExchangeRateProviderResult(CurrencyExchangeRateProviderStatus.NotFound))).ToList();

        if (fromEur && toEur)
            return EnumerateDates(start, end).Select(d => (d, new CurrencyExchangeRateProviderResult(CurrencyExchangeRateProviderStatus.Success, 1m))).ToList();

        if (fromEur || toEur)
        {
            var code = Normalize(fromEur ? toCurrency : fromCurrency);
            var (_, series) = await GetSeriesAsync(code, start, end);
            return EnumerateDates(start, end).Select(d =>
            {
                if (series.TryGetValue(d, out var codePerEur) && codePerEur > 0)
                    return (d, new CurrencyExchangeRateProviderResult(CurrencyExchangeRateProviderStatus.Success, fromEur ? codePerEur : 1m / codePerEur));
                return (d, new CurrencyExchangeRateProviderResult(CurrencyExchangeRateProviderStatus.NotFound));
            }).ToList();
        }

        var (_, fromSeries) = await GetSeriesAsync(Normalize(fromCurrency), start, end);
        var (_, toSeries) = await GetSeriesAsync(Normalize(toCurrency), start, end);
        return EnumerateDates(start, end).Select(d =>
        {
            if (fromSeries.TryGetValue(d, out var aPerEur) && aPerEur > 0 &&
                toSeries.TryGetValue(d, out var bPerEur) && bPerEur > 0)
                return (d, new CurrencyExchangeRateProviderResult(CurrencyExchangeRateProviderStatus.Success, bPerEur / aPerEur));
            return (d, new CurrencyExchangeRateProviderResult(CurrencyExchangeRateProviderStatus.NotFound));
        }).ToList();
    }

    private async Task<(bool Failed, decimal? Value)> GetObservationAsync(string code, DateTime date)
    {
        var (failed, series) = await GetSeriesAsync(code, date, date);
        if (failed) return (true, null);
        return series.TryGetValue(date.Date, out var value) ? (false, value) : (false, null);
    }

    // Returns (Failed, date→(code per EUR)). Failed marks a transport/parse error; a 404 (unsupported
    // currency or a range with no published observations) is not a failure and yields an empty map.
    private async Task<(bool Failed, Dictionary<DateTime, decimal> Series)> GetSeriesAsync(string code, DateTime start, DateTime end)
    {
        var baseUrl = options.Value.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/data/EXR/D.{code}.EUR.SP00.A?startPeriod={start:yyyy-MM-dd}&endPeriod={end:yyyy-MM-dd}&format=csvdata";

        try
        {
            using var response = await httpClient.GetAsync(url);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return (false, []);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("ECB returned {StatusCode} for {Code} {Start:yyyy-MM-dd}..{End:yyyy-MM-dd}.", response.StatusCode, code, start, end);
                return (true, []);
            }

            var content = await response.Content.ReadAsStringAsync();
            return (false, ParseCsv(content));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ECB request failed for {Code} {Start:yyyy-MM-dd}..{End:yyyy-MM-dd}.", code, start, end);
            return (true, []);
        }
    }

    // ECB csvdata is a header row followed by one observation per line; TIME_PERIOD and OBS_VALUE column
    // positions are resolved from the header so the parser tolerates column-order changes.
    private static Dictionary<DateTime, decimal> ParseCsv(string content)
    {
        Dictionary<DateTime, decimal> series = [];
        if (string.IsNullOrWhiteSpace(content)) return series;

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return series;

        var header = SplitCsvLine(lines[0]);
        var timeIndex = Array.FindIndex(header, h => h.Equals("TIME_PERIOD", StringComparison.OrdinalIgnoreCase));
        var valueIndex = Array.FindIndex(header, h => h.Equals("OBS_VALUE", StringComparison.OrdinalIgnoreCase));
        if (timeIndex < 0 || valueIndex < 0) return series;

        for (var i = 1; i < lines.Length; i++)
        {
            var fields = SplitCsvLine(lines[i]);
            if (fields.Length <= timeIndex || fields.Length <= valueIndex) continue;

            if (DateTime.TryParseExact(fields[timeIndex], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) &&
                decimal.TryParse(fields[valueIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out var value) &&
                value > 0)
                series[day.Date] = value;
        }

        return series;
    }

    private static string[] SplitCsvLine(string line) =>
        line.TrimEnd('\r').Split(',').Select(f => f.Trim().Trim('"')).ToArray();

    private static IEnumerable<DateTime> EnumerateDates(DateTime start, DateTime end)
    {
        for (var day = start; day <= end; day = day.AddDays(1))
            yield return day;
    }

    private static bool IsEur(Currency currency) => string.Equals(currency.ShortName, _eur, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(Currency currency) => currency.ShortName.Trim().ToUpperInvariant();
}