using FinanceManager.Domain.Assets.Discovery;

namespace FinanceManager.Application.FinancialAccounts.Investments.Discovery;

public interface IInvestmentInstrumentSearchService
{
    Task<IReadOnlyList<InvestmentInstrumentOptionDto>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default);

    Task<InstrumentDiscoveryResultDto?> ResolveExternalResultAsync(
        string resultId,
        CancellationToken cancellationToken = default);
}