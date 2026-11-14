using System.Globalization;

namespace FinanceManager.Domain.FinancialAccounts.Currencies.Services;

/// <summary>
/// Describes the outcome of resolving one exchange rate. Each case carries only the data that applies to it:
/// a rate on success, and a precise retry point when the provider has not published the requested current UTC
/// date yet.
/// </summary>
public closed record CurrencyExchangeRateResult
{
    public sealed record Success(decimal Value) : CurrencyExchangeRateResult;

    public sealed record NotFound : CurrencyExchangeRateResult;

    public sealed record Failed : CurrencyExchangeRateResult;

    /// <summary>The provider has not published <c>requestedDate</c> yet; retrying before <see cref="RetryAtUtc"/> is pointless.</summary>
    public sealed record NotYetPublished : CurrencyExchangeRateResult
    {
        // Not a positional record: the constructor normalizes both dates to UTC and derives the user-facing
        // message, so every instance is consistent by construction.
        public NotYetPublished(DateTime requestedDate, DateTime retryAtUtc)
        {
            var requestedDateUtc = DateTime.SpecifyKind(requestedDate.Date, DateTimeKind.Utc);
            RetryAtUtc = DateTime.SpecifyKind(retryAtUtc, DateTimeKind.Utc);
            var retryTimestamp = RetryAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            Message = $"The exchange rate for {requestedDateUtc:yyyy-MM-dd} UTC has not been published yet. It is safe to retry after {retryTimestamp}.";
        }

        /// <summary>The earliest UTC timestamp at which another request is safe.</summary>
        public DateTime RetryAtUtc { get; }

        /// <summary>A user-safe explanation of the delay.</summary>
        public string Message { get; }
    }
}