namespace FinanceManager.Application.Labels.RecurringTransactions;

/// <summary>
/// Fuzzy merchant comparison: names are reduced to lower-case letters and digits, then compared by
/// normalised Levenshtein distance (1.0 = identical).
/// </summary>
internal static class MerchantSimilarity
{
    /// <summary>Two merchants at or above this similarity are treated as the same merchant.</summary>
    public const double Threshold = 0.75;

    public static string Normalize(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    public static bool IsSameMerchant(string a, string b) => Calculate(a, b) >= Threshold;

    public static double Calculate(string a, string b)
    {
        a = Normalize(a);
        b = Normalize(b);
        if (string.Equals(a, b, StringComparison.Ordinal)) return 1.0;
        if (a.Length == 0 || b.Length == 0) return 0.0;

        var distance = LevenshteinDistance(a, b);
        return 1.0 - (double)distance / Math.Max(a.Length, b.Length);
    }

    private static int LevenshteinDistance(string s, string t)
    {
        var d = new int[s.Length + 1, t.Length + 1];
        for (var i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= t.Length; j++) d[0, j] = j;

        for (var j = 1; j <= t.Length; j++)
            for (var i = 1; i <= s.Length; i++)
                d[i, j] = s[i - 1] == t[j - 1]
                    ? d[i - 1, j - 1]
                    : 1 + Math.Min(d[i - 1, j], Math.Min(d[i, j - 1], d[i - 1, j - 1]));

        return d[s.Length, t.Length];
    }
}