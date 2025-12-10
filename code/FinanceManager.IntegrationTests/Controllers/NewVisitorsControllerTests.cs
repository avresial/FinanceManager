using FinanceManager.Components.HttpClients;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceManager.IntegrationTests.Controllers;

[Collection("api")]
public class NewVisitorsControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private TestDatabase? _testDatabase;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _testDatabase = new TestDatabase();

        // remove any registration for AppDbContext
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddSingleton(_testDatabase!.Context);
    }

    [Fact]
    public async Task AddNewVisitor_AddsVisit()
    {
        Authorize("Test user", 1, Domain.Enums.UserRole.User);
        // No auth needed for AddNewVisitor
        await new NewVisitorsHttpClient(Client).AddVisit();

        // Verify by checking the database or getting the visit
        var count = await new NewVisitorsHttpClient(Client).GetVisit(DateTime.UtcNow);
        Assert.Equal(1, count);
    }

    public override void Dispose()
    {
        base.Dispose();
        if (_testDatabase is null)
            return;

        _testDatabase.Dispose();
        _testDatabase = null;
        GC.SuppressFinalize(this);
    }
}