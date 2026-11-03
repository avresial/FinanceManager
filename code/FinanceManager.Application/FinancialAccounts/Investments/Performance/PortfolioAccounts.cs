using FinanceManager.Application.FinancialAccounts.Bond.Valuation;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;

namespace FinanceManager.Application.FinancialAccounts.Investments.Performance;

/// <summary>
/// The investment and bond accounts a portfolio return calculation covers, with the bond details
/// their entries reference.
/// </summary>
internal sealed record PortfolioAccounts(
    IReadOnlyList<InvestmentAccount> InvestmentAccounts,
    IReadOnlyList<BondAccount> BondAccounts,
    IReadOnlyDictionary<int, BondDetails> BondDetails)
{
    public bool IsEmpty => InvestmentAccounts.Count == 0 && BondAccounts.Count == 0;

    public int[] InvestmentAccountIds => [.. InvestmentAccounts.Select(account => account.AccountId)];

    /// <summary>False when a stored bond entry references details that could not be loaded.</summary>
    public bool HasAllBondDetails => BondAccounts
        .SelectMany(account => account.GetStoredBondsIds())
        .All(BondDetails.ContainsKey);

    public static async Task<PortfolioAccounts> LoadAsync(
        IFinancialAccountRepository financialAccountRepository,
        BondDashboardContext bondDashboardContext,
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

        if (investmentAccounts.Count == 0 && bondAccounts.Count == 0)
            return new(investmentAccounts, bondAccounts, new Dictionary<int, BondDetails>());

        var bondDetails = await bondDashboardContext.LoadReferencedDetailsAsync(bondAccounts, cancellationToken);
        return new(investmentAccounts, bondAccounts, bondDetails);
    }
}