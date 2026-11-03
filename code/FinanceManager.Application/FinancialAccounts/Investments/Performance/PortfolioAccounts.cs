using FinanceManager.Application.FinancialAccounts.Bond.Valuation;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;

namespace FinanceManager.Application.FinancialAccounts.Investments.Performance;

/// <summary>
/// The investment and bond accounts a portfolio return calculation covers. Bond details load
/// separately so each service keeps its own repository-call order.
/// </summary>
internal sealed record PortfolioAccounts(
    IReadOnlyList<InvestmentAccount> InvestmentAccounts,
    IReadOnlyList<BondAccount> BondAccounts)
{
    public bool IsEmpty => InvestmentAccounts.Count == 0 && BondAccounts.Count == 0;

    public int[] InvestmentAccountIds => [.. InvestmentAccounts.Select(account => account.AccountId)];

    public Task<IReadOnlyDictionary<int, BondDetails>> LoadBondDetailsAsync(
        BondDashboardContext bondDashboardContext,
        CancellationToken cancellationToken) =>
        bondDashboardContext.LoadReferencedDetailsAsync(BondAccounts, cancellationToken);

    /// <summary>False when a stored bond entry references details missing from <paramref name="bondDetails"/>.</summary>
    public bool HasAllBondDetails(IReadOnlyDictionary<int, BondDetails> bondDetails) => BondAccounts
        .SelectMany(account => account.GetStoredBondsIds())
        .All(bondDetails.ContainsKey);

    public static async Task<PortfolioAccounts> LoadAsync(
        IFinancialAccountRepository financialAccountRepository,
        int userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        List<InvestmentAccount> investmentAccounts = [];
        await foreach (var account in financialAccountRepository
                           .GetAccounts<InvestmentAccount>(userId, startDate, endDate)
                           .WithCancellation(cancellationToken))
            investmentAccounts.Add(account);

        List<BondAccount> bondAccounts = [];
        await foreach (var account in financialAccountRepository
                           .GetAccounts<BondAccount>(userId, startDate, endDate)
                           .WithCancellation(cancellationToken))
            bondAccounts.Add(account);

        return new(investmentAccounts, bondAccounts);
    }
}