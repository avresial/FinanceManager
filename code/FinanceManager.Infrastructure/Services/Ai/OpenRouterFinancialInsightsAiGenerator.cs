using FinanceManager.Application.Options;
using FinanceManager.Application.Services.FinancialInsights;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class OpenRouterFinancialInsightsAiGenerator(
    HttpClient httpClient,
    AppDbContext dbContext,
    IOptions<OpenRouterOptions> options,
    ILogger<OpenRouterFinancialInsightsAiGenerator> logger) : IFinancialInsightsAiGenerator
{
    private const int MaxEntriesPerAccount = 200;
    private const int MaxAccounts = 50;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<FinancialInsight>> GenerateInsights(int userId, int? accountId, int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0) return [];

        var model = options.Value.Model;
        if (string.IsNullOrWhiteSpace(model))
        {
            logger.LogWarning("OpenRouter model is not configured.");
            return [];
        }

        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("OpenRouter API key is not configured.");
            return [];
        }

        var baseUrl = options.Value.BaseUrl;
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            var normalizedBaseUrl = baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/";
            httpClient.BaseAddress = new Uri(normalizedBaseUrl, UriKind.Absolute);
        }

        var entriesContextJson = await BuildEntriesContextJson(userId, accountId, cancellationToken);

        var prompt = "Generate short financial insight for a personal finance dashboard. " +
                     "No disclaimers, no markdown. Output must be STRICT JSON ONLY with this shape: " +
                     "{\"insights\":[{\"title\":string,\"message\":string,\"tags\":[string]}]}. " +
                     "Keep title <= 128 chars, message <= 1024 chars. Tags: 1-3 short lowercase words. " +
                     "Use the provided user data context to make insights specific." +
                     $"\n\nUSER_DATA_CONTEXT_JSON (last 3 months per account; may be truncated):\n{entriesContextJson}";

        var request = new OpenRouterChatRequest
        {
            Model = model,
            Messages =
            [
                new OpenRouterMessage("system", "You are a finance assistant that outputs strict JSON."),
                new OpenRouterMessage("user", prompt)
            ],
            ResponseFormat = new OpenRouterResponseFormat { Type = "json_object" }
        };

        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = JsonContent.Create(request)
            };

            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OpenRouter chat request failed with status {StatusCode}", response.StatusCode);
                return [];
            }

            var payload = await response.Content.ReadFromJsonAsync<OpenRouterChatResponse>(_jsonOptions, cancellationToken);
            var content = payload?.Choices?.FirstOrDefault()?.Message?.Content;
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
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "OpenRouter insights generation timed out");
            return [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OpenRouter insights generation failed");
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

    private async Task<string> BuildEntriesContextJson(int userId, int? accountId, CancellationToken cancellationToken)
    {
        var end = DateTime.UtcNow;
        var start = end.AddMonths(-3);

        var accountsQuery = dbContext.Accounts.AsNoTracking().Where(a => a.UserId == userId);
        if (accountId.HasValue)
            accountsQuery = accountsQuery.Where(a => a.AccountId == accountId.Value);

        var accounts = await accountsQuery
            .OrderBy(a => a.AccountId)
            .Take(MaxAccounts)
            .ToListAsync(cancellationToken);

        var context = new EntriesContext
        {
            TimeRangeUtc = new TimeRangeContext { StartUtc = start, EndUtc = end },
            Accounts = []
        };

        foreach (var account in accounts)
        {
            var accountContext = new AccountEntriesContext
            {
                AccountId = account.AccountId,
                Name = account.Name,
                AccountType = account.AccountType.ToString(),
                AccountLabel = account.AccountLabel.ToString(),
                Entries = []
            };

            switch (account.AccountType)
            {
                case Domain.Enums.AccountType.Currency:
                    accountContext.Entries = await dbContext.CurrencyEntries.AsNoTracking()
                        .Where(e => e.AccountId == account.AccountId && e.PostingDate >= start && e.PostingDate <= end)
                        .OrderBy(e => e.PostingDate)
                        .Take(MaxEntriesPerAccount)
                        .Select(e => new EntryContext
                        {
                            PostingDateUtc = e.PostingDate,
                            Value = e.Value,
                            ValueChange = e.ValueChange,
                            Description = e.Description
                        })
                        .ToListAsync(cancellationToken);
                    break;

                case Domain.Enums.AccountType.Stock:
                    accountContext.Entries = await dbContext.StockEntries.AsNoTracking()
                        .Where(e => e.AccountId == account.AccountId && e.PostingDate >= start && e.PostingDate <= end)
                        .OrderBy(e => e.PostingDate)
                        .Take(MaxEntriesPerAccount)
                        .Select(e => new EntryContext
                        {
                            PostingDateUtc = e.PostingDate,
                            Value = e.Value,
                            ValueChange = e.ValueChange,
                            Ticker = e.Ticker,
                            InvestmentType = e.InvestmentType.ToString()
                        })
                        .ToListAsync(cancellationToken);
                    break;

                case Domain.Enums.AccountType.Bond:
                    accountContext.Entries = await dbContext.BondEntries.AsNoTracking()
                        .Where(e => e.AccountId == account.AccountId && e.PostingDate >= start && e.PostingDate <= end)
                        .OrderBy(e => e.PostingDate)
                        .Take(MaxEntriesPerAccount)
                        .Select(e => new EntryContext
                        {
                            PostingDateUtc = e.PostingDate,
                            Value = e.Value,
                            ValueChange = e.ValueChange,
                            BondDetailsId = e.BondDetailsId
                        })
                        .ToListAsync(cancellationToken);
                    break;
            }

            foreach (var entry in accountContext.Entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.Description))
                    entry.Description = Truncate(entry.Description.Trim(), 80);
            }

            context.Accounts.Add(accountContext);
        }

        return JsonSerializer.Serialize(context, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    private sealed class OpenRouterChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OpenRouterMessage> Messages { get; set; } = [];

        [JsonPropertyName("response_format")]
        public OpenRouterResponseFormat? ResponseFormat { get; set; }
    }

    private sealed class OpenRouterResponseFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }

    private sealed record OpenRouterMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed class OpenRouterChatResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenRouterChoice>? Choices { get; set; }
    }

    private sealed class OpenRouterChoice
    {
        [JsonPropertyName("message")]
        public OpenRouterMessage? Message { get; set; }
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

    private sealed class EntriesContext
    {
        public TimeRangeContext TimeRangeUtc { get; set; } = new();
        public List<AccountEntriesContext> Accounts { get; set; } = [];
    }

    private sealed class TimeRangeContext
    {
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
    }

    private sealed class AccountEntriesContext
    {
        public int AccountId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public string AccountLabel { get; set; } = string.Empty;
        public List<EntryContext> Entries { get; set; } = [];
    }

    private sealed class EntryContext
    {
        public DateTime PostingDateUtc { get; set; }
        public decimal Value { get; set; }
        public decimal ValueChange { get; set; }
        public string? Description { get; set; }
        public string? Ticker { get; set; }
        public string? InvestmentType { get; set; }
        public int? BondDetailsId { get; set; }
    }
}
