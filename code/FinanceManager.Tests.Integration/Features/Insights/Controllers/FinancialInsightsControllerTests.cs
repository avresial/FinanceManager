using FinanceManager.Application.Insights.Generation;
using FinanceManager.Components.Features.Insights.HttpClients;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Insights.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Insights.Controllers;

[Trait("Category", "Integration")]
public class FinancialInsightsControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private TestDatabase? _testDatabase;
    private StubInsightsAiGenerator? _generator;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _testDatabase = new TestDatabase();

        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddSingleton(_testDatabase.Context);

        _generator = new StubInsightsAiGenerator();
        services.AddSingleton<IFinancialInsightsAiGenerator>(_generator);
    }

    private sealed class StubInsightsAiGenerator : IFinancialInsightsAiGenerator
    {
        public int Calls { get; private set; }

        public Task<List<FinancialInsight>> GenerateInsights(int userId, int count, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new List<FinancialInsight>());
        }
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
        var result = await client.GetLatestAsync(3, TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Count);
        Assert.All(result, x => Assert.Equal(1, x.UserId));
        Assert.Equal("C", result[0].Title);
    }

    [Fact]
    public async Task GetLatest_ReturnsHistoricalAccountInsightsAlongsideOwnerWideOnes()
    {
        var now = DateTime.UtcNow;
        _testDatabase!.Context.FinancialInsights.AddRange(
            new FinancialInsight { UserId = 1, AccountId = 10, Title = "Legacy", Message = "M", Tags = "t1", CreatedAt = now.AddDays(-1) },
            new FinancialInsight { UserId = 1, AccountId = null, Title = "Owner wide", Message = "M", Tags = "t1", CreatedAt = now },
            new FinancialInsight { UserId = 2, AccountId = 10, Title = "Other user", Message = "M", Tags = "t1", CreatedAt = now }
        );
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Authorize("TestUser", 1, UserRole.User);

        var client = new FinancialInsightsHttpClient(Client);
        var result = await client.GetLatestAsync(3, TestContext.Current.CancellationToken);

        Assert.Equal(["Owner wide", "Legacy"], result.Select(x => x.Title));
        Assert.All(result, x => Assert.Null(x.AccountId));
    }

    [Theory]
    [InlineData("POST", "api/FinancialInsights/generate?accountId=7")]
    [InlineData("GET", "api/FinancialInsights/get-latest?accountId=7")]
    public async Task RetiredAccountScope_IsRejected_InsteadOfBecomingAnOwnerWideRequest(string method, string url)
    {
        Authorize("TestUser", 1, UserRole.User);

        var response = await Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url), TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _generator!.Calls);
    }

    [Fact]
    public async Task Generate_WithoutTheRetiredScope_StillRunsOwnerWide()
    {
        Authorize("TestUser", 1, UserRole.User);

        var response = await Client.PostAsync("api/FinancialInsights/generate?count=2", null, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, _generator!.Calls);
    }

    public override void Dispose()
    {
        _testDatabase?.Dispose();
        base.Dispose();
    }
}