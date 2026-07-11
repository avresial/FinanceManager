namespace FinanceManager.Domain.FinancialAccounts.Shared.Entities;

public static class FinancialLabelClassificationCatalog
{
    public const string SpendingNecessityKind = "SpendingNecessity";
    public const string UnknownValue = "Unknown";
    public const string EssentialValue = "Essential";
    public const string WantValue = "Want";
    public const string InvestmentValue = "Investment";

    public static bool TryNormalize(string kind, string value, out string normalizedKind, out string normalizedValue)
    {
        normalizedKind = string.Empty;
        normalizedValue = string.Empty;

        if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(value))
            return false;

        if (kind.Equals(SpendingNecessityKind, StringComparison.OrdinalIgnoreCase))
        {
            normalizedKind = SpendingNecessityKind;

            if (value.Equals(UnknownValue, StringComparison.OrdinalIgnoreCase))
            {
                normalizedValue = UnknownValue;
                return true;
            }

            if (value.Equals(EssentialValue, StringComparison.OrdinalIgnoreCase))
            {
                normalizedValue = EssentialValue;
                return true;
            }

            if (value.Equals(WantValue, StringComparison.OrdinalIgnoreCase))
            {
                normalizedValue = WantValue;
                return true;
            }

            if (value.Equals(InvestmentValue, StringComparison.OrdinalIgnoreCase))
            {
                normalizedValue = InvestmentValue;
                return true;
            }
        }

        return false;
    }

    private static string? ResolveMostFrequentValue(IEnumerable<string> values)
    {
        var counts = values
            .GroupBy(v => v)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Value, StringComparer.Ordinal)
            .ToList();

        if (counts.Count == 0)
            return null;

        if (counts.Count > 1 && counts[0].Count == counts[1].Count)
            return null;

        return counts[0].Value;
    }

    public static string? ResolveClassification(IEnumerable<FinancialLabel> labels)
    {
        var values = labels
            .SelectMany(label => label.Classifications)
            .Where(c => c.Kind == SpendingNecessityKind)
            .Select(c => c.Value);

        return ResolveMostFrequentValue(values);
    }
}