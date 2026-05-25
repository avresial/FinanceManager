using FinanceManager.Application.Providers;
using FinanceManager.Application.Services;
using FinanceManager.Application.Services.Ai;
using FinanceManager.Application.Services.Bonds;
using FinanceManager.Application.Services.Currencies;
using FinanceManager.Application.Services.Exports;
using FinanceManager.Application.Services.FinancialInsights;
using FinanceManager.Application.Services.Seeders;
using FinanceManager.Application.Services.Stocks;
using FinanceManager.Domain.Entities.Exports;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ISettingsService, SettingsService>()
                .AddScoped<PricingProvider>()
                .AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
        return services;
    }

    public static IServiceCollection AddApplicationApi(this IServiceCollection services)
    {
        services.AddScoped<ISettingsService, SettingsService>()
                .AddScoped<IMoneyFlowService, MoneyFlowService>()
                .AddScoped<IInvestmentPaycheckEstimatorService, InvestmentPaycheckEstimatorService>()
            .AddScoped<IEssentialSpendingServiceTyped, CurrencyEssentialSpendingService>()
            .AddScoped<IEssentialSpendingService, EssentialSpendingService>()
            .AddScoped<IExpenseDistributionServiceTyped, CurrencyExpenseDistributionService>()
            .AddScoped<IExpenseDistributionService, ExpenseDistributionService>()
                .AddScoped<IBalanceServiceTyped, CurrencyBalanceService>()
                .AddScoped<IBalanceServiceTyped, BondBalanceService>()
                .AddScoped<IBalanceServiceTyped, StockBalanceService>()
                .AddScoped<IBalanceService, BalanceService>()

                .AddScoped<IAssetsServiceTyped, AssetsServiceCurrency>()
                .AddScoped<IAssetsServiceTyped, AssetsServiceBond>()
                .AddScoped<IAssetsServiceTyped, AssetsServiceStock>()
                .AddScoped<IAssetsService, AssetsService>()
                .AddScoped<UsersService>()

                .AddScoped<ILiabilitiesService, LiabilitiesService>()
                .AddScoped<PricingProvider>()
                .AddScoped<GuestAccountSeeder>()
                .AddScoped<FinancialLabelSeeder>()
                .AddScoped<BondDetailsSeeder>()
                .AddScoped<ISeeder, AdminAccountSeeder>()
                .AddScoped<ISeeder, BondDetailsSeeder>(sp => sp.GetRequiredService<BondDetailsSeeder>())
                .AddScoped<ISeeder, StockDetailsSeeder>()
                .AddScoped<ISeeder, FinancialLabelSeeder>(sp => sp.GetRequiredService<FinancialLabelSeeder>())
                // .AddScoped<ISeeder, FinancialInsightsSeeder>()
                .AddScoped<IUserPlanVerifier, UserPlanVerifier>()
                .AddScoped<IAdministrationUsersService, AdministrationUsersService>()
                .AddScoped<ICurrencyExchangeRateProvider, CsvCurrencyExchangeProvider>()
                .AddScoped<ICurrencyExchangeService, CurrencyExchangeService>()
                .Decorate<ICurrencyExchangeService, CachedCurrencyExchangeService>();

        services.AddScoped<ICsvHeaderMappingService, CsvHeaderMappingService>()
                .AddScoped<ICurrencyAccountImportService, CurrencyAccountImportService>()
                .AddScoped<ICurrencyAccountExportService, CurrencyAccountExportService>()
                .AddScoped<IAccountCsvExportService<CurrencyAccountExportDto>, CurrencyAccountCsvExportService>()
                .AddScoped<ICurrencyEntryProvider, CurrencyEntryProvider>()
                .AddScoped<IStockAccountImportService, StockAccountImportService>()
                .AddScoped<IStockAccountExportService, StockAccountExportService>()
                .AddScoped<IAccountCsvExportService<StockAccountExportDto>, StockAccountCsvExportService>()
                .AddScoped<IStockEntryProvider, StockEntryProvider>()
            .AddScoped<IBondAccountImportService, BondAccountImportService>()
                .AddScoped<IBondAccountExportService, BondAccountExportService>()
                .AddScoped<IAccountCsvExportService<BondAccountExportDto>, BondAccountCsvExportService>()
                .AddScoped<IBondEntryProvider, BondEntryProvider>()
                .AddScoped<IStockPriceProvider, StockPriceProvider>()
                .AddScoped<IStockPriceBulkImportService, StockPriceBulkImportService>()
                .AddScoped<IStockUnrealizedGainLossCalculator, StockUnrealizedGainLossCalculator>()
                .AddScoped<IBondUnrealizedGainLossCalculator, BondUnrealizedGainLossCalculator>()
                .AddScoped<IBondService, BondService>()
                .AddScoped<IStockMarketService, StockMarketService>()

                .AddScoped<IFinancialInsightsAiGenerator, FinancialInsightsAiGenerator>()
                .AddScoped<ILabelSetterAiService, LabelSetterAiService>()
                .AddScoped<ILabelSuggestionAiService, LabelSuggestionAiService>()
                .AddScoped<IRecurringTransactionDetectorService, RecurringTransactionDetectorService>()
                .AddScoped<IDiversificationService, DiversificationService>()
            ;

        return services;
    }
}