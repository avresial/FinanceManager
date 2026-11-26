using FinanceManager.Components.Features.Administration.HttpClients;
using FinanceManager.Components.Features.Administration.Services;
using FinanceManager.Components.Features.Alerts.HttpClients;
using FinanceManager.Components.Features.Assets.HttpClients;
using FinanceManager.Components.Features.Currencies.HttpClients;
using FinanceManager.Components.Features.Dashboard.HttpClients;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.Identity.HttpClients;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.Insights.HttpClients;
using FinanceManager.Components.Features.Labels.HttpClients;
using FinanceManager.Components.Features.Labels.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Features.Portfolio.HttpClients;
using FinanceManager.Components.Features.TransactionRules.HttpClients;
using FinanceManager.Components.Features.Transactions.Bonds.HttpClients;
using FinanceManager.Components.Features.Transactions.Cash.HttpClients;
using FinanceManager.Components.Features.Transactions.Cash.Services;
using FinanceManager.Components.Features.Transactions.Import.HttpClients;
using FinanceManager.Components.Features.Transactions.Investments.HttpClients;
using FinanceManager.Components.Features.Transactions.Shared.Services;
using FinanceManager.Components.Shared.Services;
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
                .AddScoped<IBrowserCookieReader, BrowserCookieReader>()
                .AddScoped<ILoginService, LoginService>()

                .AddScoped<AssetHttpClient>()

                .AddScoped<InstrumentSearchHttpClient>()
                .AddScoped<InvestmentValuationHttpClient>()

                .AddScoped<DividendPaymentHttpClient>()

                .AddScoped<BondDetailsHttpClient>()

                .AddScoped<MoneyFlowHttpClient>()
                .AddScoped<EssentialSpendingHttpClient>()
                .AddScoped<AssetsHttpClient>()
                .AddScoped<LiabilitiesHttpClient>()
                .AddScoped<UnconvertedCurrenciesHttpClient>()
                .AddScoped<UserHttpClient>()
                .AddScoped<GiftCodeHttpClient>()
                .AddScoped<PasswordResetHttpClient>()
                .AddScoped<FinancialLabelHttpClient>()
                .AddScoped<LabelSetterProgressHttpClient>()
                .AddScoped<FinancialInsightsHttpClient>()
                .AddScoped<RecurringTransactionDetectorHttpClient>()
                .AddScoped<TransactionLogHttpClient>()
                .AddScoped<CashTransactionsHttpClient>()
                .AddScoped<BondTransactionsHttpClient>()
                .AddScoped<InvestmentTradesHttpClient>()
                .AddScoped<TransactionRuleHttpClient>()
                .AddScoped<DiversificationHttpClient>()
                .AddScoped<AdministrationUsersHttpClient>()
                .AddScoped<AdminAiProvidersHttpClient>()
                .AddScoped<AdminServiceKeysHttpClient>()
                .AddScoped<AdminMaintenanceKeyHttpClient>()
                .AddScoped<AdminLogsHttpClient>()
                .AddScoped<NewVisitorsHttpClient>()
                .AddScoped<FinancialAlertsHttpClient>()
                .AddScoped<CashFlowForecastHttpClient>()
                .AddScoped<CsvHeaderMappingHttpClient>()
                .AddScoped<TransactionDataSynchronizationService>()
                .AddScoped<NavMenuStateCacheService>()
                .AddScoped<DashboardHttpClient>()
                .AddScoped<DashboardOverviewCardsCacheService>()
                .AddScoped<DashboardCardVisibilityService>()
                .AddScoped<ISnapshotService, LocalStorageSnapshotService>()
                .AddScoped<ISnapshotRefreshCoordinator, SnapshotRefreshCoordinator>()
                .AddScoped<PageActivityTracker>()
                .AddScoped<LiabilitiesSnapshotStore>()
                .AddScoped<DashboardCardsSnapshotStore>()
                .AddScoped<DiversificationSnapshotStore>()
                .AddScoped<SubscriptionsSnapshotStore>()
                .AddScoped<AdminDashboardSnapshotStore>()
                .AddTransient<CurrencyImportJobTracker>()
                .AddScoped<AssetsPageCardsRequestCoordinator>()
                .AddScoped<CurrencyHttpClient>()
                .AddScoped<IUserService, UserService>()
                .AddScoped<UserSettingsService>()
                .AddScoped<ISettingsService>(sp => sp.GetRequiredService<UserSettingsService>());

        return services;
    }
}