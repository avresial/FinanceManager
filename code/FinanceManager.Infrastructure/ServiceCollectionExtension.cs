using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Providers;
using FinanceManager.Infrastructure.Repositories;
using FinanceManager.Infrastructure.Repositories.Account;
using FinanceManager.Infrastructure.Repositories.Account.Entry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddInfrastructureFrontend(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserLocalStorageRepository>();

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
                .AddScoped<INewVisitsRepository, NewVisitsRepository>()
                .AddScoped<IFinancialLabelsRepository, FinancialLabelsRepository>()
                .AddScoped<ICurrencyRepository, CurrencyRepository>()
                .AddScoped<IInflationDataProvider, InMemoryInflationDataProvider>()

                //.AddHostedService<DatabaseInitializer>()
                //.AddHostedService<AdminAccountSeederBackgroundService>()
                //.AddHostedService<GuestAccountSeederBackgroundService>()
                ;

        return services;
    }

    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfigurationManager Configuration)
    {
        if (Configuration.GetValue("UseInMemoryDatabase", false))
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(databaseName: "Db"));
        }
        else
        {
            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly("FinanceManager.Api")));
        }
        return services;
    }
    public static void ApplyMigrations(this IServiceScope scope)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        if (!dbContext.Database.IsRelational()) return;

        var pendingMigrations = dbContext.Database.GetPendingMigrations();
        if (pendingMigrations.Any())
        {
            logger.LogInformation("Applying pending migrations...");
            dbContext.Database.Migrate();
            logger.LogInformation("Migrations applied successfully.");
        }
        else
        {
            logger.LogInformation("No pending migrations found.");
        }
    }
    public static T GetOptions<T>(this IConfiguration configuration, string sectionName) where T : class, new()
    {
        var section = configuration.GetSection(sectionName) ?? throw new ArgumentException($"Configuration section '{sectionName}' not found.");
        var options = new T();
        section.Bind(options);
        return options;
    }
}