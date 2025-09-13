using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinanceManager.Infrastructure;
internal class DatabaseInitializer(IServiceProvider serviceProvider) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (dbContext.Database.IsRelational()) dbContext.Database.Migrate();

        var financialLabels = dbContext.FinancialLabels.ToList();
        if (financialLabels.Any())
            return Task.CompletedTask;

        financialLabels = [new() { Name = "Sallary" }];

        dbContext.FinancialLabels.AddRange(financialLabels);
        dbContext.SaveChanges();

        return Task.CompletedTask;

    }

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
