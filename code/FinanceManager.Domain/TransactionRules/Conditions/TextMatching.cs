using System.Text.RegularExpressions;

namespace FinanceManager.Domain.TransactionRules.Conditions;

/// <summary>
/// Shared text-matching logic for <see cref="ContractorCondition"/> and
/// <see cref="DescriptionCondition"/>. Values are trimmed before comparison and
/// compared ordinally (optionally case-insensitively) so matching is deterministic
/// across cultures and machines.
/// </summary>
internal static class TextMatching
{
    public static bool Matches(string value, string pattern, TextMatchOperator matchOperator, bool ignoreCase)
    {
        var trimmedValue = value.Trim();
        var trimmedPattern = pattern.Trim();

        return matchOperator switch
        {
            TextMatchOperator.Equals => trimmedValue.Equals(trimmedPattern, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
            TextMatchOperator.Contains => trimmedValue.Contains(trimmedPattern, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
            TextMatchOperator.StartsWith => trimmedValue.StartsWith(trimmedPattern, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
            TextMatchOperator.EndsWith => trimmedValue.EndsWith(trimmedPattern, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
            TextMatchOperator.RegularExpression => new Regex(trimmedPattern, ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None).IsMatch(trimmedValue),
            _ => throw new ArgumentOutOfRangeException(nameof(matchOperator), matchOperator, "Unknown text match operator."),
        };
    }

    public static void ThrowIfInvalidPattern(string pattern, bool ignoreCase)
    {
        if (pattern is null)
            throw new ArgumentNullException(nameof(pattern));

        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("Pattern must not be empty or whitespace.", nameof(pattern));

        // Fails fast on a malformed pattern for the RegularExpression operator.
        new Regex(pattern, ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
    }
}