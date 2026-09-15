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
    private static readonly TimeSpan _regexTimeout = TimeSpan.FromMilliseconds(100);

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
            TextMatchOperator.RegularExpression => MatchesRegularExpression(trimmedValue, trimmedPattern, ignoreCase),
            _ => throw new ArgumentOutOfRangeException(nameof(matchOperator), matchOperator, "Unknown text match operator."),
        };
    }

    private static bool MatchesRegularExpression(string value, string pattern, bool ignoreCase)
    {
        try
        {
            return new Regex(pattern, GetRegexOptions(ignoreCase), _regexTimeout).IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            // A user-supplied pattern must not be able to stall imports or retroactive rule runs.
            return false;
        }
    }

    public static void ThrowIfInvalidPattern(string pattern, bool ignoreCase)
    {
        if (pattern is null)
            throw new ArgumentNullException(nameof(pattern));

        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("Pattern must not be empty or whitespace.", nameof(pattern));

        // Fails fast on a malformed pattern for the RegularExpression operator.
        new Regex(pattern, GetRegexOptions(ignoreCase), _regexTimeout);
    }

    private static RegexOptions GetRegexOptions(bool ignoreCase) =>
        ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
}