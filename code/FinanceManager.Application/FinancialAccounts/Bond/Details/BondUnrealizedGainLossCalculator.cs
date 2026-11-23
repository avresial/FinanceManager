using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Application.FinancialAccounts.Bond.Details;

internal class BondUnrealizedGainLossCalculator(ICurrencyExchangeService currencyExchangeService) : IBondUnrealizedGainLossCalculator
{
    private const string _bondsGroupName = "Bonds";

    public async Task<UnrealizedGainLossInstrumentResult?> CalculateAsync(OwnerLedgerPosition position, BondDetails? details, Currency currency, DateTime asOfDate)
    {
        // A pooled balance above zero is only reachable through purchases, so no separate buy scan is needed.
        if (position.Value <= 0m) return null;

        if (details is null)
            return Excluded(position, $"Bond {position.PartitionId}", asOfDate, "Missing bond details. Excluded from unrealized totals.");

        var exchangeRate = details.Currency == currency
            ? 1m
            : await currencyExchangeService.GetExchangeRateAsync(details.Currency, currency, asOfDate) ?? 0m;

        if (exchangeRate <= 0)
            return Excluded(position, details.Name, asOfDate, "Missing FX rate for selected currency. Excluded from unrealized totals.");

        var weightedAverageCostPerUnit = details.UnitValue * exchangeRate;
        var quantity = position.Value;
        var costBasis = quantity * weightedAverageCostPerUnit;

        var anchorDate = DateOnly.FromDateTime(position.LastPostingDate);
        var asOf = DateOnly.FromDateTime(asOfDate);
        var currentValueRaw = asOf < anchorDate
            ? 0m
            : BondAccountEntry.GetPrice(anchorDate, quantity, asOf, details).GetValueOrDefault(asOf);
        var currentValue = currentValueRaw * exchangeRate;
        var gainLoss = new GainLoss(costBasis, currentValue);

        return new UnrealizedGainLossInstrumentResult(
            null,
            _bondsGroupName,
            position.PartitionId.ToString(),
            details.Name,
            quantity,
            costBasis,
            currentValue,
            gainLoss.Amount,
            gainLoss.Percent,
            asOfDate,
            false,
            null
        );
    }

    private static UnrealizedGainLossInstrumentResult Excluded(OwnerLedgerPosition position, string name, DateTime asOfDate, string warning) =>
        new(null, _bondsGroupName, position.PartitionId.ToString(), name, position.Value, 0, 0, 0, 0, asOfDate, true, warning);
}