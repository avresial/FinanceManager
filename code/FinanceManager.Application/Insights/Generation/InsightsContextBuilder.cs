using FinanceManager.Application.FinancialAccounts.Shared.Exports;
using FinanceManager.Domain.Entities.Exports;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Exports;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Exports;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Exports;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;
using System.Text;

namespace FinanceManager.Application.Insights.Generation;

internal sealed class InsightsContextBuilder(
    ICurrencyAccountRepository<CurrencyAccount> currencyAccountRepository,
    IAccountRepository<StockAccount> stockAccountRepository,
    IAccountRepository<BondAccount> bondAccountRepository,
    IAccountCsvExportService<CurrencyAccountExportDto> currencyAccountCsvExportService,
    IAccountCsvExportService<StockAccountExportDto> stockAccountCsvExportService,
    IAccountCsvExportService<BondAccountExportDto> bondAccountCsvExportService)
{
    private const int _maxEntriesPerAccount = 200;
    private const int _maxAccounts = 50;

    public async Task<string> BuildEntriesContextCsv(int userId, int? accountId, CancellationToken cancellationToken = default)
    {
        var end = DateTime.UtcNow;
        var start = end.AddMonths(-3);

        var sb = new StringBuilder();
        sb.AppendLine($"timeRangeUtcStart,{start:O}");
        sb.AppendLine($"timeRangeUtcEnd,{end:O}");

        var addedAccounts = 0;

        addedAccounts = await AppendAccountsSection(sb, currencyAccountRepository.GetAvailableAccounts(userId), "Currency",
            exportAccountId => currencyAccountCsvExportService.GetExportResults(userId, exportAccountId, start, end, cancellationToken),
            accountId, addedAccounts, cancellationToken);

        addedAccounts = await AppendAccountsSection(sb, stockAccountRepository.GetAvailableAccounts(userId), "Stock",
            exportAccountId => stockAccountCsvExportService.GetExportResults(userId, exportAccountId, start, end, cancellationToken),
            accountId, addedAccounts, cancellationToken);

        addedAccounts = await AppendAccountsSection(sb, bondAccountRepository.GetAvailableAccounts(userId), "Bond",
            exportAccountId => bondAccountCsvExportService.GetExportResults(userId, exportAccountId, start, end, cancellationToken),
            accountId, addedAccounts, cancellationToken);

        return sb.ToString();
    }

    private static async Task<int> AppendAccountsSection(
        StringBuilder sb,
        IAsyncEnumerable<AvailableAccount> accounts,
        string accountType,
        Func<int, Task<string>> exportCsv,
        int? accountId,
        int addedAccounts,
        CancellationToken cancellationToken)
    {
        await foreach (var account in accounts.OrderBy(x => x.AccountId).WithCancellation(cancellationToken))
        {
            if (addedAccounts >= _maxAccounts)
                break;

            if (accountId.HasValue && account.AccountId != accountId.Value)
                continue;

            var csv = await exportCsv(account.AccountId);

            sb.AppendLine();
            sb.AppendLine("[account]");
            sb.AppendLine($"accountId,{account.AccountId}");
            sb.AppendLine($"name,{EscapeCsvValue(account.AccountName)}");
            sb.AppendLine($"accountType,{accountType}");
            sb.AppendLine("csv:");
            sb.AppendLine(Truncate(csv, _maxEntriesPerAccount * 220));

            addedAccounts++;
        }

        return addedAccounts;
    }

    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string Truncate(string value, int maxLen) =>
        value.Length <= maxLen ? value : value[..maxLen];
}