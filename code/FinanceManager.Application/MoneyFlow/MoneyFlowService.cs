using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.MoneyFlow;

/// <summary>
/// Temporary facade kept for consumers of <see cref="IMoneyFlowService"/>;
/// the calculations live in the focused NetWorth, LabelsValue and InvestmentRate services.
/// </summary>
public class MoneyFlowService(INetWorthService netWorthService, ILabelsValueService labelsValueService,
IInvestmentRateService investmentRateService) : IMoneyFlowService
{
    public Task<decimal?> GetNetWorth(int userId, Currency currency, DateTime date) =>
        netWorthService.GetNetWorth(userId, currency, date);

    public Task<Dictionary<DateTime, decimal>> GetNetWorth(int userId, Currency currency, DateTime start, DateTime end) =>
        netWorthService.GetNetWorth(userId, currency, start, end);

    public Task<List<NameValueResult>> GetLabelsValue(int userId, DateTime start, DateTime end) =>
        labelsValueService.GetLabelsValue(userId, start, end);

    public IAsyncEnumerable<Domain.Entities.MoneyFlowModels.InvestmentRate> GetInvestmentRate(int userId, DateTime start, DateTime end) =>
        investmentRateService.GetInvestmentRate(userId, start, end);
}