using FinanceManager.Domain.MoneyFlow.Services;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceManager.Infrastructure.Features.MoneyFlow.Providers;

public class EurostatInflationIndexProvider(
    HttpClient httpClient,
    ILogger<EurostatInflationIndexProvider> logger) : IInflationIndexProvider
{
    public async Task<IReadOnlyDictionary<DateTime, decimal>> GetIndexSeriesAsync(
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default)
    {
        if (start == default || end == default || end < start)
            return new Dictionary<DateTime, decimal>();

        var path = "prc_hicp_minr?freq=M&unit=I25&coicop18=TOTAL&geo=PL"
            + $"&sinceTimePeriod={start:yyyy-MM}&untilTimePeriod={end:yyyy-MM}";

        try
        {
            var response = await httpClient.GetFromJsonAsync<EurostatResponse>(path, cancellationToken);
            if (response?.Value is null || response.Dimension?.Time?.Category?.Index is null)
                return new Dictionary<DateTime, decimal>();

            var result = new SortedDictionary<DateTime, decimal>();
            foreach (var (period, position) in response.Dimension.Time.Category.Index)
            {
                if (!response.Value.TryGetValue(position.ToString(CultureInfo.InvariantCulture), out var value)
                    || !DateTime.TryParseExact(period, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    || value <= 0)
                    continue;

                result[date.Date < start.Date ? start.Date : date.Date] = value;
            }

            if (result.Count > 0 && result.Keys.Last() < end.Date)
                result[end.Date] = result.Values.Last();

            return result;
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Eurostat inflation-index request cancelled.");
            throw;
        }
        catch (OperationCanceledException ex)
        {
            logger.LogDebug(ex, "Eurostat inflation-index request cancelled or timed out.");
            return new Dictionary<DateTime, decimal>();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Eurostat did not return the Polish inflation index for {Start:d}–{End:d}", start, end);
            return new Dictionary<DateTime, decimal>();
        }
    }

    private sealed class EurostatResponse
    {
        [JsonPropertyName("value")]
        public Dictionary<string, decimal>? Value { get; set; }

        [JsonPropertyName("dimension")]
        public EurostatDimension? Dimension { get; set; }
    }

    private sealed class EurostatDimension
    {
        [JsonPropertyName("time")]
        public EurostatTime? Time { get; set; }
    }

    private sealed class EurostatTime
    {
        [JsonPropertyName("category")]
        public EurostatCategory? Category { get; set; }
    }

    private sealed class EurostatCategory
    {
        [JsonPropertyName("index")]
        public Dictionary<string, int>? Index { get; set; }
    }
}