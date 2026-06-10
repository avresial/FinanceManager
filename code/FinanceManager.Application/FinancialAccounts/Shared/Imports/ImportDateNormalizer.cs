namespace FinanceManager.Application.FinancialAccounts.Shared.Imports;

// Posting dates are stored and compared at second precision across the app.
// Truncating both sides keeps legacy fractional-second DB entries comparable
// with second-precision CSV imports. Centralized here so currency, stock and
// bond imports normalize dates identically.
public static class ImportDateNormalizer
{
    public static DateTime ToSecond(DateTime date) =>
        new(date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second, date.Kind);
}