using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Repositories;
using FinanceManager.Infrastructure.Repositories.Account;
using FinanceManager.Infrastructure.Repositories.Account.Entry;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Infrastructure
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddInfrastructureFrontend(this IServiceCollection services)
        {
            services.AddScoped<IStockRepository, StockRepositoryMock>()
                    .AddScoped<IUserRepository, UserLocalStorageRepository>();

            return services;
        }

        public static IServiceCollection AddInfrastructureApi(this IServiceCollection services)
        {
            services.AddSingleton<ActiveUsersContext>()
                    .AddScoped<IFinancalAccountRepository, AccountRepository>()
                    .AddScoped<IStockRepository, StockRepositoryMock>()
                    .AddSingleton<IUserRepository, UserInMemoryRepository>()
                    .AddSingleton<IActiveUsersRepository, ActiveUsersRepository>()
                    .AddSingleton<IAccountEntryRepository<BankAccountEntry>, InMemoryBankEntryRepository>()
                    .AddSingleton<IAccountEntryRepository<StockAccountEntry>, InMemoryStockEntryRepository>()
                    .AddSingleton<IAccountRepository<StockAccount>, InMemoryStockAccountRepository>()
                    .AddSingleton<IBankAccountRepository<BankAccount>, InMemoryBankAccountRepository>()
                    ;

            return services;
        }
    }
}
