namespace FinanceManager.Infrastructure.Features.Transactions.Shared;

internal static class OwnerLedgerTimestamps
{
    /// <summary>Posting dates are stored as UTC, so local timestamps are converted and unspecified ones are taken as UTC.</summary>
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}