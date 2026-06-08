namespace FinanceManager.Api;

internal static class RequestBodySizeLimits
{
    public const long GlobalRequestBodyBytes = 50 * 1024 * 1024;
    public const int ImportEndpointBytes = 30 * 1024 * 1024;

    public static long GetLimitForPath(PathString path)
    {
        var value = path.Value;
        if (value is null)
            return GlobalRequestBodyBytes;

        return value.Equals("/api/BondAccountImport/ImportBondEntries", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/api/CurrencyAccountImport/StartAsyncImport", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/api/CurrencyAccountImport/ImportCurrencyEntries", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/api/StockAccountImport/ImportStockEntries", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/api/StockPrice/bulk-import-close-prices", StringComparison.OrdinalIgnoreCase)
            ? ImportEndpointBytes
            : GlobalRequestBodyBytes;
    }
}
