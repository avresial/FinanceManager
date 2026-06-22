namespace FinanceManager.Application.FinancialAccounts.Stock.Resolution;

/// <summary>
/// Detects whether a free-form resolution input is a structured security identifier (ISIN) so the
/// resolver can take a deterministic, direct lookup path instead of the fuzzy ticker fan-out.
/// </summary>
public static class InstrumentIdentifier
{
    /// <summary>
    /// True when <paramref name="input"/> is a syntactically valid ISIN: two country letters, nine
    /// alphanumeric characters, and a final check digit that satisfies the ISO 6166 Luhn mod-10
    /// checksum. The checksum guards against treating an ordinary ticker that happens to be twelve
    /// characters long as an ISIN.
    /// </summary>
    public static bool IsIsin(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var value = input.Trim().ToUpperInvariant();
        if (value.Length != 12)
            return false;

        // First two characters must be the country prefix letters; the next nine alphanumeric; the
        // last a digit.
        if (!char.IsAsciiLetterUpper(value[0]) || !char.IsAsciiLetterUpper(value[1]))
            return false;

        for (var i = 2; i < 11; i++)
        {
            if (!char.IsAsciiLetterOrDigit(value[i]))
                return false;
        }

        if (!char.IsAsciiDigit(value[11]))
            return false;

        return HasValidLuhnCheckDigit(value);
    }

    // Expand each character to its digit(s) — letters map A=10 … Z=35 and contribute two digits —
    // then run the Luhn mod-10 algorithm over the whole expanded string including the check digit.
    private static bool HasValidLuhnCheckDigit(string isin)
    {
        var sum = 0;
        var doubleNext = false;

        // Walk right-to-left so the doubling alignment is anchored on the check digit.
        for (var i = isin.Length - 1; i >= 0; i--)
        {
            var c = isin[i];
            var expanded = char.IsAsciiDigit(c)
                ? ((int)(c - '0')).ToString()
                : (c - 'A' + 10).ToString();

            for (var j = expanded.Length - 1; j >= 0; j--)
            {
                var digit = expanded[j] - '0';
                if (doubleNext)
                {
                    digit *= 2;
                    if (digit > 9)
                        digit -= 9;
                }

                sum += digit;
                doubleNext = !doubleNext;
            }
        }

        return sum % 10 == 0;
    }
}