using FinanceManager.Core.Repositories;
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
                    .AddScoped<ILoginRepository, LoginRepository>();

            return services;
        }
    }
}
