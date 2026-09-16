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
    private const int _maxRegexPatternLength = 1_000;
    private const int _maxRegexNestingDepth = 20;
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

    public static void ThrowIfInvalidPattern(string pattern, TextMatchOperator matchOperator, bool ignoreCase)
    {
        if (pattern is null)
            throw new ArgumentNullException(nameof(pattern));

        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("Pattern must not be empty or whitespace.", nameof(pattern));

        if (!Enum.IsDefined(matchOperator))
            throw new ArgumentOutOfRangeException(nameof(matchOperator), matchOperator, "Unknown text match operator.");

        if (matchOperator == TextMatchOperator.RegularExpression)
        {
            ThrowIfRegexIsTooComplex(pattern);
            new Regex(pattern, GetRegexOptions(ignoreCase), _regexTimeout);
        }
    }

    private static void ThrowIfRegexIsTooComplex(string pattern)
    {
        if (pattern.Length > _maxRegexPatternLength)
            throw new ArgumentException($"Regular expression patterns must not exceed {_maxRegexPatternLength} characters.", nameof(pattern));

        var nestingDepth = 0;
        var isEscaped = false;
        var isInCharacterClass = false;

        foreach (var character in pattern)
        {
            if (isEscaped)
            {
                isEscaped = false;
                continue;
            }

            if (character == '\\')
            {
                isEscaped = true;
                continue;
            }

            if (character == '[')
            {
                isInCharacterClass = true;
                continue;
            }

            if (character == ']' && isInCharacterClass)
            {
                isInCharacterClass = false;
                continue;
            }

            if (isInCharacterClass)
                continue;

            if (character == '(')
            {
                nestingDepth++;
                if (nestingDepth > _maxRegexNestingDepth)
                    throw new ArgumentException($"Regular expression patterns must not exceed {_maxRegexNestingDepth} levels of nesting.", nameof(pattern));
            }
            else if (character == ')' && nestingDepth > 0)
            {
                nestingDepth--;
            }
        }
    }

    private static RegexOptions GetRegexOptions(bool ignoreCase) =>
        RegexOptions.CultureInvariant
        | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
}