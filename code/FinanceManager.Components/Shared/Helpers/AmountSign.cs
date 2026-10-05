using MudBlazor;

namespace FinanceManager.Components.Shared.Helpers;

/// <summary>Sign and colour conventions for signed amounts in lists: income is green, expense is red.</summary>
public static class AmountSign
{
    public static Color ColorFor(decimal amount) => amount switch
    {
        > 0m => Color.Success,
        < 0m => Color.Error,
        _ => Color.Default
    };

    /// <summary>"+" for positive amounts; negative numbers already carry their own "-".</summary>
    public static string PrefixFor(decimal amount) => amount > 0m ? "+" : string.Empty;
}