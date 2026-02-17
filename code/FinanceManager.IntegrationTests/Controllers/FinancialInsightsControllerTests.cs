using FinanceManager.Application.Services.FinancialInsights;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceManager.IntegrationTests.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class FinancialInsightsControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private TestDatabase? _testDatabase;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _testDatabase = new TestDatabase();

        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddSingleton(_testDatabase.Context);

        services.AddSingleton<IFinancialInsightsAiGenerator>(new StubInsightsAiGenerator());
    }

    private sealed class StubInsightsAiGenerator : IFinancialInsightsAiGenerator
    {
        public Task<List<FinancialInsight>> GenerateInsights(int userId, int? accountId, int count, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<FinancialInsight>());
    }

    [Fact]
    public async Task GetLatest_ReturnsOnlyUserInsights()
    {
        var now = DateTime.UtcNow;
        _testDatabase!.Context.FinancialInsights.AddRange(
            new FinancialInsight { UserId = 1, Title = "A", Message = "M", Tags = "t1", CreatedAt = now.AddDays(-2) },
            new FinancialInsight { UserId = 1, Title = "B", Message = "M", Tags = "t1", CreatedAt = now.AddDays(-1) },
            new FinancialInsight { UserId = 1, Title = "C", Message = "M", Tags = "t1", CreatedAt = now },
            new FinancialInsight { UserId = 2, Title = "Other", Message = "M", Tags = "t2", CreatedAt = now }
        );
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Authorize("TestUser", 1, UserRole.User);

        var client = new FinancialInsightsHttpClient(Client);
        var result = await client.GetLatestAsync(3, null, TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Count);
        Assert.All(result, x => Assert.Equal(1, x.UserId));
        Assert.Equal("C", result[0].Title);
    }

    [Fact]
    public async Task GetLatest_WithAccountFilter_ReturnsFilteredInsights()
    {
        var now = DateTime.UtcNow;
        _testDatabase!.Context.FinancialInsights.AddRange(
            new FinancialInsight { UserId = 1, AccountId = 10, Title = "A", Message = "M", Tags = "t1", CreatedAt = now.AddDays(-1) },
            new FinancialInsight { UserId = 1, AccountId = 20, Title = "B", Message = "M", Tags = "t1", CreatedAt = now },
            new FinancialInsight { UserId = 1, AccountId = 20, Title = "C", Message = "M", Tags = "t1", CreatedAt = now.AddDays(-2) }
        );
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Authorize("TestUser", 1, UserRole.User);

        var client = new FinancialInsightsHttpClient(Client);
        var result = await client.GetLatestAsync(3, 20, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.All(result, x => Assert.Equal(20, x.AccountId));
        Assert.Equal("B", result[0].Title);
    }

    public override void Dispose()
    {
        _testDatabase?.Dispose();
        base.Dispose();
    }
}