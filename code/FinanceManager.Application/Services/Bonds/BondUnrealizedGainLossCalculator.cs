using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services.Bonds;

internal class BondUnrealizedGainLossCalculator(ICurrencyExchangeService currencyExchangeService) : IBondUnrealizedGainLossCalculator
{
    public async Task<UnrealizedGainLossInstrumentResult?> CalculateAsync(BondAccount account, int bondDetailsId, BondDetails? details, Currency currency, DateTime asOfDate)
    {
        var latestEntry = account.GetThisOrNextOlder(asOfDate, bondDetailsId);
        if (latestEntry is null || latestEntry.Value <= 0) return null;

        if (details is null)
        {
            return new UnrealizedGainLossInstrumentResult(
                account.AccountId,
                account.Name,
                bondDetailsId.ToString(),
                $"Bond {bondDetailsId}",
                latestEntry.Value,
                0,
                0,
                0,
                0,
                asOfDate,
                true,
                "Missing bond details. Excluded from account unrealized totals."
            );
        }

        var buyEntries = account.Entries
            .Where(x => x.BondDetailsId == bondDetailsId && x.PostingDate <= asOfDate && x.ValueChange > 0)
            .ToList();

        decimal totalBoughtUnits = buyEntries.Sum(x => x.ValueChange);
        if (totalBoughtUnits <= 0)
        {
            return new UnrealizedGainLossInstrumentResult(
                account.AccountId,
                account.Name,
                bondDetailsId.ToString(),
                details.Name,
                latestEntry.Value,
                0,
                0,
                0,
                0,
                asOfDate,
                true,
                "Missing buy transactions. Excluded from account unrealized totals."
            );
        }

        var exchangeRate = details.Currency == currency
            ? 1m
            : await currencyExchangeService.GetExchangeRateAsync(details.Currency, currency, asOfDate) ?? 0m;

        if (exchangeRate <= 0)
        {
            return new UnrealizedGainLossInstrumentResult(
                account.AccountId,
                account.Name,
                bondDetailsId.ToString(),
                details.Name,
                latestEntry.Value,
                0,
                0,
                0,
                0,
                asOfDate,
                true,
                "Missing FX rate for selected currency. Excluded from account unrealized totals."
            );
        }

        var weightedAverageCostPerUnit = details.UnitValue * exchangeRate;
        var quantity = latestEntry.Value;
        var costBasis = quantity * weightedAverageCostPerUnit;

        var currentValueRaw = latestEntry.GetPriceAt(DateOnly.FromDateTime(asOfDate), details);
        var currentValue = currentValueRaw * exchangeRate;
        var unrealized = currentValue - costBasis;
        var unrealizedPercent = costBasis == 0 ? 0 : unrealized / costBasis * 100;

        return new UnrealizedGainLossInstrumentResult(
            account.AccountId,
            account.Name,
            bondDetailsId.ToString(),
            details.Name,
            quantity,
            costBasis,
            currentValue,
            unrealized,
            unrealizedPercent,
            asOfDate,
            false,
            null
        );
    }
}