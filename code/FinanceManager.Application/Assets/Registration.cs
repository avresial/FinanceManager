using FinanceManager.Application.Assets.Discovery;
using FinanceManager.Application.Assets.Pricing;
using FinanceManager.Application.Backfill;
using FinanceManager.Domain.Assets.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application.Assets;

internal static class Registration
{
    public static IServiceCollection AddAssetsApplication(this IServiceCollection services)
    {
        services.AddScoped<IPriceQuoteIngestionService, PriceQuoteIngestionService>()
                .AddScoped<IInvestmentPriceProvider, InvestmentPriceProvider>()
                .AddScoped<IInvestmentPriceBackfillService, InvestmentPriceBackfillService>()
                .AddScoped<IBackfillService, StockPriceBackfillService>()
                .AddScoped<IInvestmentInstrumentDiscoveryService, InvestmentInstrumentDiscoveryService>()
                .AddScoped<IInvestmentInstrumentSearchService, InvestmentInstrumentSearchService>()
                .AddScoped<MarketDataSymbolResolutionService>()
                .AddScoped<IInstrumentImportService, InstrumentImportService>()
                .AddScoped<AdminAssetDiscoveryService>()
                .AddScoped<IncompleteAssetQueueService>();

        return services;
    }
}