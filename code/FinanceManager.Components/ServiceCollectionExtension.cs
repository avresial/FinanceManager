using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Infrastructure.Repositories;
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
                .AddScoped<DiversificationHttpClient>()
                .AddScoped<AdministrationUsersHttpClient>()
                .AddScoped<AdminAiProvidersHttpClient>()
                .AddScoped<AdminServiceKeysHttpClient>()
                .AddScoped<AdminLogsHttpClient>()
                .AddScoped<NewVisitorsHttpClient>()
                .AddScoped<CsvHeaderMappingHttpClient>()
                .AddScoped<AccountDataSynchronizationService>()
                .AddScoped<NavMenuStateCacheService>()
                .AddScoped<DashboardHttpClient>()
                .AddScoped<DashboardOverviewCardsCacheService>()
                .AddScoped<DashboardCardVisibilityService>()
                .AddScoped<ISnapshotService, LocalStorageSnapshotService>()
                .AddScoped<AccountDetailsSnapshotStore>()
                .AddTransient<CurrencyImportJobTracker>()
                .AddScoped<AssetsPageCardsCacheService>()
                .AddScoped<InvestmentPaycheckEstimateCacheService>()
                .AddScoped<CurrencyHttpClient>()
                .AddScoped<IUserService, UserService>()
                .AddScoped<IFinancialAccountService, FinancialAccountService>()
                .AddScoped<UserSettingsService>()
                .AddScoped<ISettingsService>(sp => sp.GetRequiredService<UserSettingsService>())
                .AddScoped<IUserRepository, UserLocalStorageRepository>();
        ;

        return services;
    }
}