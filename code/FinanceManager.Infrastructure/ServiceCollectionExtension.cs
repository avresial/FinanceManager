using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Contexts;
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
            services.AddDbContext<AppDbContext>()
                    .AddScoped<IStockRepository, StockRepositoryMock>()
                    .AddScoped<IFinancalAccountRepository, AccountRepository>()
                    .AddScoped<IUserRepository, UserInMemoryRepository>()
                    .AddScoped<IActiveUsersRepository, ActiveUsersRepository>()
                    .AddScoped<IAccountEntryRepository<BankAccountEntry>, InMemoryBankEntryRepository>()
                    .AddScoped<IAccountEntryRepository<StockAccountEntry>, InMemoryStockEntryRepository>()
                    .AddScoped<IAccountRepository<StockAccount>, InMemoryStockAccountRepository>()
                    .AddScoped<IBankAccountRepository<BankAccount>, InMemoryBankAccountRepository>()
                    .AddScoped<NewVisitsRepository>()

                    ;

            return services;
        }
    }
}
