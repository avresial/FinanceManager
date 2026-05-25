namespace FinanceManager.Domain.Entities.Shared.Accounts;

public static class WellKnownFinancialLabels
{
    // Sentinel applied by the AI label setter when no real category fits an entry.
    // Without this, those entries stay unlabelled and get re-queued forever.
    public const string NoMatch = "No matching label";
}