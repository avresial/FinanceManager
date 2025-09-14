using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinanceManager.Infrastructure;
internal class DatabaseInitializer(IServiceProvider serviceProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (dbContext.Database.IsRelational()) await dbContext.Database.MigrateAsync(cancellationToken);

        var financialLabels = await dbContext.FinancialLabels.ToListAsync(cancellationToken);
        if (financialLabels.Count != 0) return;

        financialLabels = [new() { Name = "Salary" }];

        dbContext.FinancialLabels.AddRange(financialLabels);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
