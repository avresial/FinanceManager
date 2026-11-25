using FinanceManager.Application.Transactions.Bonds.Valuation;
using FinanceManager.Domain.Currencies.Repositories;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Cash.Repositories;

namespace FinanceManager.Application.MoneyFlow.NetWorth;

/// <summary>
/// Cash balances and bond holdings are summed in their own currency (only investments are converted), so any held currency
/// other than the display one makes the headline figures approximate. This names those currencies so the UI can say so.
/// </summary>
public class UnconvertedCurrencyService(
    ICashTransactionReader cashTransactionReader,
    IBondTransactionReader bondTransactionReader,
    BondDashboardContext bondDashboardContext,
    ICurrencyRepository currencyRepository) : IUnconvertedCurrencyService
{
    public async Task<IReadOnlyList<string>> GetCodes(int userId, int displayCurrencyId, CancellationToken cancellationToken = default)
    {
        var asOf = DateTime.UtcNow;
        var display = (await currencyRepository.GetCurrency(displayCurrencyId, cancellationToken))?.ShortName;
        HashSet<string> codes = new(StringComparer.OrdinalIgnoreCase);

        var cash = (await cashTransactionReader.GetPositions(userId, asOf, cancellationToken)).Where(p => p.Value != 0m).ToList();
        if (cash.Count > 0)
        {
            var names = await currencyRepository.GetCurrencies().ToDictionaryAsync(currency => currency.Id, currency => currency.ShortName);
            foreach (var position in cash)
                if (names.GetValueOrDefault(position.PartitionId) is string code) codes.Add(code);
        }

        var bonds = (await bondTransactionReader.GetPositions(userId, asOf, cancellationToken)).Where(p => p.Value != 0m).ToList();
        if (bonds.Count > 0)
        {
            await PooledBondValuation.LoadDetailsAsync(bondDashboardContext, bonds, cancellationToken);
            foreach (var position in bonds)
                codes.Add(bondDashboardContext.GetDetails(position.PartitionId).Currency.ShortName);
        }

        if (display is not null) codes.Remove(display);
        return [.. codes.Order(StringComparer.OrdinalIgnoreCase)];
    }
}