using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;

namespace FinanceManager.Components.Features.TransactionRules.Services;

/// <summary>
/// Display names for the accounts a rule can target. Accounts that share a name are disambiguated by
/// their label and, if that is still ambiguous, by their position among the duplicates.
/// </summary>
/// <param name="Accounts">The user's available accounts.</param>
/// <param name="Loaded">False when accounts failed to load, so unknown ids are not reported as deleted.</param>
internal sealed record TransactionRuleAccountLabels(IReadOnlyList<AvailableAccount> Accounts, bool Loaded)
{
    public static TransactionRuleAccountLabels NotLoaded { get; } = new([], false);

    public bool IsMissing(int accountId) => Loaded && Accounts.All(account => account.AccountId != accountId);

    public string Get(AvailableAccount account)
    {
        var accountsWithSameName = Accounts
            .Where(candidate => candidate.AccountName.Equals(account.AccountName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate.AccountId)
            .ToList();
        var context = account.AccountLabel?.ToString();
        if (accountsWithSameName.Count == 1)
            return context is null ? account.AccountName : $"{account.AccountName} ({context})";

        var duplicateContext = accountsWithSameName.Count(candidate => candidate.AccountLabel == account.AccountLabel) > 1;
        if (!duplicateContext && context is not null)
            return $"{account.AccountName} ({context})";

        var position = accountsWithSameName.IndexOf(account) + 1;
        return context is null
            ? $"{account.AccountName} ({position} of {accountsWithSameName.Count})"
            : $"{account.AccountName} ({context} {position} of {accountsWithSameName.Count})";
    }

    public string Get(int accountId)
    {
        var account = Accounts.FirstOrDefault(candidate => candidate.AccountId == accountId);
        if (account is not null) return Get(account);
        return Loaded ? Missing(accountId) : $"Account #{accountId}";
    }

    public static string Missing(int accountId) => $"Deleted account (#{accountId})";
}