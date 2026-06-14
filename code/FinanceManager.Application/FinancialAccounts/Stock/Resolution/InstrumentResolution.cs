namespace FinanceManager.Application.FinancialAccounts.Stock.Resolution;

/// <summary>
/// Result of instrument resolution: either an auto-resolved match or a list of candidates for user confirmation.
/// </summary>
public sealed class InstrumentResolution
{
    public required ResolutionKind Kind { get; set; }
    public InstrumentListing? Match { get; set; }
    public IReadOnlyList<InstrumentListing> Candidates { get; set; } = [];
}

public enum ResolutionKind
{
    /// <summary>Single unambiguous match found and persisted automatically.</summary>
    AutoResolved = 0,

    /// <summary>Multiple candidates found; user must confirm which one.</summary>
    Ambiguous = 1,

    /// <summary>No match found across all providers.</summary>
    NoMatch = 2
}