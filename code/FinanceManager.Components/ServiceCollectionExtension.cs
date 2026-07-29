using FinanceManager.Components.Features.Administration.HttpClients;
using FinanceManager.Components.Features.Dashboard.HttpClients;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.Identity.HttpClients;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.Insights.HttpClients;
using FinanceManager.Components.Features.Labels.HttpClients;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Components;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddUIComponents(this IServiceCollection services)
    {
        services.AddMemoryCache();

        services.AddScoped<IAntiforgeryTokenService, AntiforgeryTokenService>()
                .AddScoped<ILoginService, LoginService>()

                .AddScoped<InvestmentAccountHttpClient>()
                .AddScoped<AssetHttpClient>()

                .AddScoped<InvestmentTransactionHttpClient>()
                .AddScoped<InvestmentInstrumentDiscoveryHttpClient>()
                .AddScoped<InvestmentValuationHttpClient>()

                .AddScoped<CurrencyAccountHttpClient>()
                .AddScoped<CurrencyAccountImportHttpClient>()
                .AddScoped<CurrencyEntryHttpClient>()

                .AddScoped<BondAccountHttpClient>()
                .AddScoped<BondAccountImportHttpClient>()
                .AddScoped<BondEntryHttpClient>()
                .AddScoped<BondDetailsHttpClient>()

                .AddScoped<MoneyFlowHttpClient>()
                .AddScoped<EssentialSpendingHttpClient>()
                .AddScoped<AssetsHttpClient>()
                .AddScoped<LiabilitiesHttpClient>()
                .AddScoped<UserHttpClient>()
                .AddScoped<PasswordResetHttpClient>()
                .AddScoped<FinancialLabelHttpClient>()
                .AddScoped<LabelSetterProgressHttpClient>()
                .AddScoped<FinancialInsightsHttpClient>()
                .AddScoped<RecurringTransactionDetectorHttpClient>()
                .AddScoped<TransactionLogHttpClient>()
                .AddScoped<DiversificationHttpClient>()
                .AddScoped<AdministrationUsersHttpClient>()
                .AddScoped<AdminAiProvidersHttpClient>()
                .AddScoped<AdminServiceKeysHttpClient>()
                .AddScoped<AdminMaintenanceKeyHttpClient>()
                .AddScoped<AdminLogsHttpClient>()
                .AddScoped<NewVisitorsHttpClient>()
                .AddScoped<CsvHeaderMappingHttpClient>()
                .AddScoped<AccountDataSynchronizationService>()
                .AddScoped<NavMenuStateCacheService>()
                .AddScoped<DashboardHttpClient>()
                .AddScoped<DashboardOverviewCardsCacheService>()
                .AddScoped<DashboardCardVisibilityService>()
                .AddScoped<ISnapshotService, LocalStorageSnapshotService>()
                .AddScoped<ISnapshotRefreshCoordinator, SnapshotRefreshCoordinator>()
                .AddScoped<AccountDetailsSnapshotStore>()
                .AddScoped<LiabilitiesSnapshotStore>()
                .AddScoped<DiversificationSnapshotStore>()
                .AddTransient<CurrencyImportJobTracker>()
                .AddScoped<AssetsPageCardsCacheService>()
                .AddScoped<InvestmentPaycheckEstimateCacheService>()
                .AddScoped<InvestmentRateCacheService>()
                .AddScoped<CurrencyHttpClient>()
                .AddScoped<IUserService, UserService>()
                .AddScoped<IFinancialAccountService, FinancialAccountService>()
                .AddScoped<UserSettingsService>()
                .AddScoped<ISettingsService>(sp => sp.GetRequiredService<UserSettingsService>());

        return services;
    }
}