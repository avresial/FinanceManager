using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace FinanceManager.Api;

internal static class RequestBodySizeLimits
{
    public const long GlobalRequestBodyBytes = 50L * 1024 * 1024;
    public const long ImportEndpointBytes = 30L * 1024 * 1024;

    public const string BondImportPath = "/api/BondAccountImport/ImportBondEntries";
    public const string CurrencyStartAsyncImportPath = "/api/CurrencyAccountImport/StartAsyncImport";
    public const string CurrencyImportEntriesPath = "/api/CurrencyAccountImport/ImportCurrencyEntries";

    private static readonly HashSet<string> _importEndpointPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        BondImportPath,
        CurrencyStartAsyncImportPath,
        CurrencyImportEntriesPath,
    };

    public static long GetLimitForPath(PathString path)
    {
        var value = path.Value;
        if (value is null)
            return GlobalRequestBodyBytes;

        return _importEndpointPaths.Contains(value)
            ? ImportEndpointBytes
            : GlobalRequestBodyBytes;
    }
}