using FinanceManager.Application.Services.Exports;
using FinanceManager.Application.Services.FinancialInsights;
using FinanceManager.Application.Services.Ai;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Exports;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Repositories.Account;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class GitHubModelsFinancialInsightsAiGenerator(
    ICurrencyAccountRepository<CurrencyAccount> currencyAccountRepository,
    IAccountRepository<StockAccount> stockAccountRepository,
    IAccountRepository<BondAccount> bondAccountRepository,
    IAccountCsvExportService<CurrencyAccountExportDto> currencyAccountCsvExportService,
    IAccountCsvExportService<StockAccountExportDto> stockAccountCsvExportService,
    IAccountCsvExportService<BondAccountExportDto> bondAccountCsvExportService,
    IInsightsPromptProvider insightsPromptProvider,
    IAiProvider aiProvider,
    ILogger<GitHubModelsFinancialInsightsAiGenerator> logger) : IFinancialInsightsAiGenerator
{
    private const int _maxEntriesPerAccount = 200;
    private const int _maxAccounts = 50;
    private const string _systemPrompt = "You are a finance assistant that outputs strict JSON.";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<FinancialInsight>> GenerateInsights(int userId, int? accountId, int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0) return [];

        var entriesContextCsv = await BuildEntriesContextCsv(userId, accountId, cancellationToken);
        var prompt = await insightsPromptProvider.BuildPromptAsync(entriesContextCsv, cancellationToken);

        try
        {
            var content = await aiProvider.Get(_systemPrompt, prompt, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
                return [];

            var parsed = TryParseInsights(content);
            if (parsed.Count == 0)
                return [];

            var now = DateTime.UtcNow;
            var result = new List<FinancialInsight>(Math.Min(count, parsed.Count));

            foreach (var item in parsed.Take(count))
            {
                var title = Truncate(item.Title?.Trim() ?? string.Empty, 128);
                var message = Truncate(item.Message?.Trim() ?? string.Empty, 1024);
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
                    continue;

                var tags = NormalizeTags(item.Tags);

                result.Add(new FinancialInsight
                {
                    UserId = userId,
                    AccountId = accountId,
                    Title = title,
                    Message = message,
                    Tags = Truncate(string.Join(',', tags), 256),
                    CreatedAt = now
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GitHub Models insights generation failed");
            return [];
        }
    }

    private static List<InsightItem> TryParseInsights(string content)
    {
        var trimmed = content.Trim();

        if (TryDeserialize(trimmed, out var parsed))
            return parsed;

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            var candidate = trimmed[start..(end + 1)];
            if (TryDeserialize(candidate, out parsed))
                return parsed;
        }

        return [];

        static bool TryDeserialize(string json, out List<InsightItem> items)
        {
            items = [];
            try
            {
                var root = JsonSerializer.Deserialize<InsightsRoot>(json, _jsonOptions);
                if (root?.Insights is null) return false;
                items = root.Insights;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private static IReadOnlyList<string> NormalizeTags(List<string>? tags)
    {
        if (tags is null || tags.Count == 0)
            return ["summary"];

        var cleaned = tags
            .Select(t => (t ?? string.Empty).Trim().ToLowerInvariant())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .Take(3)
            .ToList();

        return cleaned.Count == 0 ? ["summary"] : cleaned;
    }

    private static string Truncate(string value, int maxLen) =>
        value.Length <= maxLen ? value : value[..maxLen];

    private async Task<string> BuildEntriesContextCsv(int userId, int? accountId, CancellationToken cancellationToken)
    {
        var end = DateTime.UtcNow;
        var start = end.AddMonths(-3);

        var sb = new StringBuilder();
        sb.AppendLine($"timeRangeUtcStart,{start:O}");
        sb.AppendLine($"timeRangeUtcEnd,{end:O}");

        var addedAccounts = 0;

        await foreach (var account in currencyAccountRepository.GetAvailableAccounts(userId).OrderBy(x => x.AccountId).WithCancellation(cancellationToken))
        {
            if (addedAccounts >= _maxAccounts)
                break;

            if (accountId.HasValue && account.AccountId != accountId.Value)
                continue;

            var csv = await currencyAccountCsvExportService.GetExportResults(userId, account.AccountId, start, end, cancellationToken);

            sb.AppendLine();
            sb.AppendLine("[account]");
            sb.AppendLine($"accountId,{account.AccountId}");
            sb.AppendLine($"name,{EscapeCsvValue(account.AccountName)}");
            sb.AppendLine("accountType,Currency");
            sb.AppendLine("csv:");
            sb.AppendLine(Truncate(csv, _maxEntriesPerAccount * 220));

            addedAccounts++;
        }

        await foreach (var account in stockAccountRepository.GetAvailableAccounts(userId).OrderBy(x => x.AccountId).WithCancellation(cancellationToken))
        {
            if (addedAccounts >= _maxAccounts)
                break;

            if (accountId.HasValue && account.AccountId != accountId.Value)
                continue;

            var csv = await stockAccountCsvExportService.GetExportResults(userId, account.AccountId, start, end, cancellationToken);

            sb.AppendLine();
            sb.AppendLine("[account]");
            sb.AppendLine($"accountId,{account.AccountId}");
            sb.AppendLine($"name,{EscapeCsvValue(account.AccountName)}");
            sb.AppendLine("accountType,Stock");
            sb.AppendLine("csv:");
            sb.AppendLine(Truncate(csv, _maxEntriesPerAccount * 220));

            addedAccounts++;
        }

        await foreach (var account in bondAccountRepository.GetAvailableAccounts(userId).OrderBy(x => x.AccountId).WithCancellation(cancellationToken))
        {
            if (addedAccounts >= _maxAccounts)
                break;

            if (accountId.HasValue && account.AccountId != accountId.Value)
                continue;

            var csv = await bondAccountCsvExportService.GetExportResults(userId, account.AccountId, start, end, cancellationToken);

            sb.AppendLine();
            sb.AppendLine("[account]");
            sb.AppendLine($"accountId,{account.AccountId}");
            sb.AppendLine($"name,{EscapeCsvValue(account.AccountName)}");
            sb.AppendLine("accountType,Bond");
            sb.AppendLine("csv:");
            sb.AppendLine(Truncate(csv, _maxEntriesPerAccount * 220));

            addedAccounts++;
        }

        return sb.ToString();
    }

    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private sealed class InsightsRoot
    {
        [JsonPropertyName("insights")]
        public List<InsightItem> Insights { get; set; } = [];
    }

    private sealed class InsightItem
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }
    }
}
