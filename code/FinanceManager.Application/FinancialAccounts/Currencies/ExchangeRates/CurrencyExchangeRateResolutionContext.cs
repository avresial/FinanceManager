using FinanceManager.Domain.FinancialAccounts.Currencies.Services;

namespace FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;

/// <summary>
/// Carries request-scoped provider state across direct and USD-cross lookups without sharing state
/// between unrelated requests.
/// </summary>
public sealed class CurrencyExchangeRateResolutionContext
{
    private readonly Dictionary<DateTime, HashSet<ICurrencyExchangeRateProvider>> _outOfRangeProviders = [];

    public HashSet<ICurrencyExchangeRateProvider> GetOutOfRangeProviders(DateTime date)
    {
        var normalizedDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        return _outOfRangeProviders.TryGetValue(normalizedDate, out var providers)
            ? providers
            : _outOfRangeProviders[normalizedDate] = [];
    }
}