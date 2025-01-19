using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Infrastructure
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddScoped<IFinancalAccountRepository, InMemoryMockAccountRepository>()
                    .AddScoped<IStockRepository, StockRepositoryMock>()
                    .AddScoped<IUserRepository, UserLocalStorageRepository>();

            return services;
        }

        public static IServiceCollection AddInfrastructureApi(this IServiceCollection services)
        {
            services.AddScoped<IFinancalAccountRepository, InMemoryMockAccountRepository>()
                    .AddScoped<IStockRepository, StockRepositoryMock>()
                    .AddSingleton<IUserRepository, UserInMemoryRepository>()
                    .AddSingleton<IBankAccountRepository, InMemoryBankAccountRepository>()
                    ;

            return services;
        }
    }
}
