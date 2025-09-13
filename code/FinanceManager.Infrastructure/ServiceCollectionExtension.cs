using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Repositories;
using FinanceManager.Infrastructure.Repositories.Account;
using FinanceManager.Infrastructure.Repositories.Account.Entry;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Infrastructure;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddInfrastructureFrontend(this IServiceCollection services)
    {
        services//.AddScoped<IStockRepository, StockRepositoryMock>()
                .AddScoped<IUserRepository, UserLocalStorageRepository>();

        return services;
    }

    public static IServiceCollection AddInfrastructureApi(this IServiceCollection services)
    {

        services
                .AddScoped<IStockPriceRepository, StockPriceRepository>()
                .AddScoped<IFinancialAccountRepository, AccountRepository>()
                .AddScoped<IUserRepository, UserRepository>()
                .AddScoped<IActiveUsersRepository, ActiveUsersRepository>()
                .AddScoped<IAccountEntryRepository<BankAccountEntry>, BankEntryRepository>()
                .AddScoped<IStockAccountEntryRepository<StockAccountEntry>, StockEntryRepository>()
                .AddScoped<IAccountRepository<StockAccount>, StockAccountRepository>()
                .AddScoped<IBankAccountRepository<BankAccount>, BankAccountRepository>()
                .AddScoped<NewVisitsRepository>()
                .AddScoped<IDuplicateEntryRepository, DuplicateEntryRepository>()
                .AddScoped<IFinancialLabelsRepository, FinancialLabelsRepository>()
                .AddHostedService<DatabaseInitializer>()
                ;

        return services;
    }

}
