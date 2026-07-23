using FinanceManager.Application.Assets.Pricing;
using FinanceManager.Application.Backfill;
using FinanceManager.Application.Backfill.Currencies;
using FinanceManager.Application.FinancialAccounts.Bond;
using FinanceManager.Application.FinancialAccounts.Bond.Assets;
using FinanceManager.Application.FinancialAccounts.Bond.Balance;
using FinanceManager.Application.FinancialAccounts.Bond.Details;
using FinanceManager.Application.FinancialAccounts.Bond.Export;
using FinanceManager.Application.FinancialAccounts.Bond.Import;
using FinanceManager.Application.FinancialAccounts.Bond.Seeders;
using FinanceManager.Application.FinancialAccounts.Currencies;
using FinanceManager.Application.FinancialAccounts.Currencies.Assets;
using FinanceManager.Application.FinancialAccounts.Currencies.Balance;
using FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;
using FinanceManager.Application.FinancialAccounts.Currencies.Export;
using FinanceManager.Application.FinancialAccounts.Currencies.Import;
using FinanceManager.Application.FinancialAccounts.Currencies.Seeders;
using FinanceManager.Application.FinancialAccounts.Investments.Assets;
using FinanceManager.Application.FinancialAccounts.Investments.Balance;
using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Application.FinancialAccounts.Investments.Seeders;
using FinanceManager.Application.FinancialAccounts.Investments.Valuation;
using FinanceManager.Application.FinancialAccounts.Shared.Csv;
using FinanceManager.Application.FinancialAccounts.Shared.Exports;
using FinanceManager.Application.FinancialAccounts.Shared.Imports;
using FinanceManager.Application.Shared.Seeders;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Exports;
using FinanceManager.Domain.FinancialAccounts.Bond.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Exports;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Exports;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application.FinancialAccounts;

internal static class Registration
{
    public static IServiceCollection AddFinancialAccountsApplication(this IServiceCollection services)
    {
        services.AddScoped<IBalanceServiceTyped, CurrencyBalanceService>()
                .AddScoped<IBalanceServiceTyped, BondBalanceService>()
                .AddScoped<IBalanceServiceTyped, InvestmentBalanceService>()
                .AddScoped<IBalanceService, BalanceService>()

                .AddScoped<IAssetsServiceTyped, AssetsServiceCurrency>()
                .AddScoped<IAssetsServiceTyped, AssetsServiceBond>()
                .AddScoped<IAssetsServiceTyped, AssetsServiceInvestment>()
                .AddScoped<IAssetsService, AssetsService>()

                .AddScoped<CurrencyAccountSeeder>()
                .AddScoped<InvestmentAccountSeeder>()
                .AddScoped<BondAccountSeeder>()
                .AddScoped<BondDetailsSeeder>()
                .AddScoped<ISeeder, BondDetailsSeeder>(sp => sp.GetRequiredService<BondDetailsSeeder>())

                .AddScoped<ICurrencyExchangeRateProvider, CsvCurrencyExchangeProvider>()
                .AddScoped<ICurrencyExchangeService, CurrencyExchangeService>()
                .Decorate<ICurrencyExchangeService, CachedCurrencyExchangeService>();

        services.AddScoped<ImportAccountValidator>()
                .AddScoped<ICsvHeaderMappingService, CsvHeaderMappingService>()
                .AddScoped<ICurrencyAccountImportService, CurrencyAccountImportService>()
                .AddScoped<ICurrencyAccountExportService, CurrencyAccountExportService>()
                .AddScoped<IAccountCsvExportService<CurrencyAccountExportDto>, CurrencyAccountCsvExportService>()
                .AddScoped<IBondAccountImportService, BondAccountImportService>()
                .AddScoped<IBondAccountExportService, BondAccountExportService>()
                .AddScoped<IAccountCsvExportService<BondAccountExportDto>, BondAccountCsvExportService>()
                .AddScoped<IInvestmentPriceProvider, InvestmentPriceProvider>()
                .AddScoped<IInvestmentPriceBackfillService, InvestmentPriceBackfillService>()
                .AddScoped<ICurrencyPairDiscoveryService, CurrencyPairDiscoveryService>()
                .AddScoped<IBackfillService, StockPriceBackfillService>()
                .AddScoped<IBackfillService, CurrencyRateBackfillService>()
                .AddScoped<IInvestmentValuationService, InvestmentValuationService>()
                .AddScoped<IInvestmentTransactionValuationService, InvestmentTransactionValuationService>()
                .AddScoped<IInvestmentInstrumentDiscoveryService, InvestmentInstrumentDiscoveryService>()
                .AddScoped<IInstrumentImportService, InstrumentImportService>()
                .AddScoped<IBondUnrealizedGainLossCalculator, BondUnrealizedGainLossCalculator>()
                .AddScoped<IBondService, BondService>();

        return services;
    }
}