using FinanceManager.Application.Shared.Seeders;
using FinanceManager.Application.Transactions.Bonds.Details;
using FinanceManager.Application.Transactions.Bonds.Seeders;
using FinanceManager.Application.Transactions.Bonds.Valuation;
using FinanceManager.Application.Transactions.Cash.Seeders;
using FinanceManager.Application.Transactions.Export;
using FinanceManager.Application.Transactions.Import;
using FinanceManager.Application.Transactions.Investments;
using FinanceManager.Application.Transactions.Investments.Seeders;
using FinanceManager.Domain.Transactions.Bonds.Exports;
using FinanceManager.Domain.Transactions.Bonds.Services;
using FinanceManager.Domain.Transactions.Cash.Exports;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application.Transactions;

internal static class Registration
{
    public static IServiceCollection AddTransactionsApplication(this IServiceCollection services)
    {
        services.AddScoped<BondDashboardContext>()
                .AddScoped<CashEntrySeeder>()
                .AddScoped<InvestmentTransactionSeeder>()
                .AddScoped<BondEntrySeeder>()
                .AddScoped<BondDetailsSeeder>()
                .AddScoped<ISeeder, BondDetailsSeeder>(sp => sp.GetRequiredService<BondDetailsSeeder>())
                .AddScoped<ImportEntryLimitValidator>()
                .AddScoped<ICsvHeaderMappingService, CsvHeaderMappingService>()
                .AddScoped<ICashImportService, CashImportService>()
                .AddScoped<ICashExportService, CashExportService>()
                .AddScoped<ICsvExportService<CashExportDto>, CashCsvExportService>()
                .AddScoped<IBondImportService, BondImportService>()
                .AddScoped<IBondExportService, BondExportService>()
                .AddScoped<ICsvExportService<BondExportDto>, BondCsvExportService>()
                .AddScoped<IInvestmentTransactionService, InvestmentTransactionService>()
                .AddScoped<InvestmentTransactionPriceRecoveryService>()
                .AddScoped<IBondUnrealizedGainLossCalculator, BondUnrealizedGainLossCalculator>()
                .AddScoped<IBondService, BondService>();

        return services;
    }
}