using FinanceManager.Application.Backfill;
using FinanceManager.Application.Backfill.Currencies;
using FinanceManager.Application.Currencies.ExchangeRates;
using FinanceManager.Domain.Currencies.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application.Currencies;

internal static class Registration
{
    public static IServiceCollection AddCurrenciesApplication(this IServiceCollection services)
    {
        services.AddScoped<ICurrencyExchangeRateProvider, CsvCurrencyExchangeProvider>()
                .AddScoped<ICurrencyExchangeRateSource, CurrencyExchangeRateProviderSource>()
                .Decorate<ICurrencyExchangeRateSource, StoredCurrencyExchangeRateSource>()
                .Decorate<ICurrencyExchangeRateSource, CachedCurrencyExchangeRateSource>()
                .AddScoped<ICurrencyExchangeService, CurrencyExchangeService>()
                .AddScoped<ICurrencyPairDiscoveryService, CurrencyPairDiscoveryService>()
                .AddScoped<IBackfillService, CurrencyRateBackfillService>();

        return services;
    }
}