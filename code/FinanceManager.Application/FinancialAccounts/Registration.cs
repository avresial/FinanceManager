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
using FinanceManager.Application.FinancialAccounts.Shared.Csv;
using FinanceManager.Application.FinancialAccounts.Shared.Exports;
using FinanceManager.Application.FinancialAccounts.Shared.Imports;
using FinanceManager.Application.FinancialAccounts.Stock;
using FinanceManager.Application.FinancialAccounts.Stock.Assets;
using FinanceManager.Application.FinancialAccounts.Stock.Balance;
using FinanceManager.Application.FinancialAccounts.Stock.Export;
using FinanceManager.Application.FinancialAccounts.Stock.Import;
using FinanceManager.Application.FinancialAccounts.Stock.Market;
using FinanceManager.Application.FinancialAccounts.Stock.Pricing;
using FinanceManager.Application.FinancialAccounts.Stock.Seeders;
using FinanceManager.Application.Shared.Seeders;
using FinanceManager.Domain.Entities.Exports;
using FinanceManager.Domain.FinancialAccounts.Bond.Services;
using FinanceManager.Domain.FinancialAccounts.Stock.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application.FinancialAccounts;

internal static class Registration
{
    public static IServiceCollection AddFinancialAccountsApplication(this IServiceCollection services)
    {
        services.AddScoped<IBalanceServiceTyped, CurrencyBalanceService>()
                .AddScoped<IBalanceServiceTyped, BondBalanceService>()
                .AddScoped<IBalanceServiceTyped, StockBalanceService>()
                .AddScoped<IBalanceService, BalanceService>()

                .AddScoped<IAssetsServiceTyped, AssetsServiceCurrency>()
                .AddScoped<IAssetsServiceTyped, AssetsServiceBond>()
                .AddScoped<IAssetsServiceTyped, AssetsServiceStock>()
                .AddScoped<IAssetsService, AssetsService>()

                .AddScoped<CurrencyAccountSeeder>()
                .AddScoped<StockAccountSeeder>()
                .AddScoped<BondAccountSeeder>()
                .AddScoped<BondDetailsSeeder>()
                .AddScoped<ISeeder, BondDetailsSeeder>(sp => sp.GetRequiredService<BondDetailsSeeder>())
                .AddScoped<ISeeder, StockDetailsSeeder>()

                .AddScoped<ICurrencyExchangeRateProvider, CsvCurrencyExchangeProvider>()
                .AddScoped<ICurrencyExchangeService, CurrencyExchangeService>()
                .Decorate<ICurrencyExchangeService, CachedCurrencyExchangeService>();

        services.AddScoped<ImportAccountValidator>()
                .AddScoped<ICsvHeaderMappingService, CsvHeaderMappingService>()
                .AddScoped<ICurrencyAccountImportService, CurrencyAccountImportService>()
                .AddScoped<ICurrencyAccountExportService, CurrencyAccountExportService>()
                .AddScoped<IAccountCsvExportService<CurrencyAccountExportDto>, CurrencyAccountCsvExportService>()
                .AddScoped<IStockAccountImportService, StockAccountImportService>()
                .AddScoped<IStockAccountExportService, StockAccountExportService>()
                .AddScoped<IAccountCsvExportService<StockAccountExportDto>, StockAccountCsvExportService>()
                .AddScoped<IBondAccountImportService, BondAccountImportService>()
                .AddScoped<IBondAccountExportService, BondAccountExportService>()
                .AddScoped<IAccountCsvExportService<BondAccountExportDto>, BondAccountCsvExportService>()
                .AddScoped<IStockPriceProvider, StockPriceProvider>()
                .AddScoped<IStockPriceBulkImportService, StockPriceBulkImportService>()
                .AddScoped<IStockUnrealizedGainLossCalculator, StockUnrealizedGainLossCalculator>()
                .AddScoped<IBondUnrealizedGainLossCalculator, BondUnrealizedGainLossCalculator>()
                .AddScoped<IBondService, BondService>()
                .AddScoped<IStockMarketService, StockMarketService>();

        return services;
    }
}