namespace FinanceManager.Domain.Entities.Shared.Accounts;

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
}