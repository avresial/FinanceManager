using System.Globalization;

namespace FinanceManager.Domain.FinancialAccounts.Currencies.Services;

/// <summary>
/// Describes the outcome of resolving one exchange rate, including a precise retry point when the
/// provider has not published the requested current UTC date yet.
/// </summary>
public sealed record CurrencyExchangeRateResult
{
    private CurrencyExchangeRateResult(
        CurrencyExchangeRateStatus status,
        decimal? value = null,
        DateTime? retryAtUtc = null,
        string? message = null)
    {
        Status = status;
        Value = value;
        RetryAtUtc = retryAtUtc;
        Message = message;
    }

    public CurrencyExchangeRateStatus Status { get; }

    public decimal? Value { get; }

    /// <summary>The earliest UTC timestamp at which another request is safe.</summary>
    public DateTime? RetryAtUtc { get; }

    /// <summary>A user-safe explanation for a non-success outcome.</summary>
    public string? Message { get; }

    public bool IsSuccess => Status == CurrencyExchangeRateStatus.Success && Value is not null;

    public static CurrencyExchangeRateResult Success(decimal value) =>
        new(CurrencyExchangeRateStatus.Success, value);

    public static CurrencyExchangeRateResult NotFound() =>
        new(CurrencyExchangeRateStatus.NotFound);

    public static CurrencyExchangeRateResult Failed() =>
        new(CurrencyExchangeRateStatus.Failed);

    public static CurrencyExchangeRateResult NotYetPublished(DateTime requestedDate, DateTime retryAtUtc)
    {
        var requestedDateUtc = DateTime.SpecifyKind(requestedDate.Date, DateTimeKind.Utc);
        var normalizedRetryAtUtc = DateTime.SpecifyKind(retryAtUtc, DateTimeKind.Utc);
        var retryTimestamp = normalizedRetryAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        return new(
            CurrencyExchangeRateStatus.NotYetPublished,
            retryAtUtc: normalizedRetryAtUtc,
            message: $"The exchange rate for {requestedDateUtc:yyyy-MM-dd} UTC has not been published yet. It is safe to retry after {retryTimestamp}.");
    }
}