using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Currencies.Commands;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Commands;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using FinanceManager.Tests.Integration.Support.LegacyAccountClients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.FinancialAccounts.Currencies.Controllers;

[Trait("Category", "Integration")]
public class CurrencyEntryControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private const int _testUserId = 88;
    private const int _testAccountId = 456;
    private TestDatabase? _testDatabase;

    protected override void ConfigureServices(IServiceCollection services)
    {
        // Replace DbContext with in-memory test context
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        _testDatabase = new TestDatabase();
        // Requests and the label setter run concurrently. Share the database, not an EF context.
        var databaseOptions = (DbContextOptions<AppDbContext>)_testDatabase.Context.GetService<IDbContextOptions>();
        services.AddScoped(_ => new AppDbContext(databaseOptions));

        // Mock user plan verifier to allow any number of entries
        var planVerifierMock = new Mock<IUserPlanVerifier>();
        planVerifierMock.Setup(x => x.CanAddMoreEntries(_testUserId, It.IsAny<int>())).ReturnsAsync(true);
        services.AddSingleton(planVerifierMock.Object);
    }

    private async Task SeedAccount()
    {
        if (_testDatabase is null) return;
        if (await _testDatabase.Context.Accounts.AnyAsync(a => a.AccountId == _testAccountId, TestContext.Current.CancellationToken)) return;
        _testDatabase.Context.Accounts.Add(new FinancialAccountBaseDto
        {
            AccountId = _testAccountId,
            UserId = _testUserId,
            Name = "Test Currency Account",
            AccountLabel = AccountLabel.Other,
            AccountType = AccountType.Currency
        });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedEntry(long entryId, DateTime postingDate, decimal value, decimal valueChange, string description = "Test entry", string? contractorDetails = null)
    {
        if (_testDatabase is null) return;
        _testDatabase.Context.CurrencyEntries.Add(new CurrencyAccountEntry(_testAccountId, entryId, postingDate, value, valueChange)
        {
            Description = description,
            ContractorDetails = contractorDetails,
            Labels = []
        });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetEntry_ReturnsEntry_WhenExists()
    {
        // arrange
        await SeedAccount();
        var entryId = 1;
        var postingDate = DateTime.UtcNow.Date.AddDays(-5);
        var value = 100m;
        var valueChange = 50m;
        await SeedEntry(entryId, postingDate, value, valueChange);
        Authorize("user", _testUserId, UserRole.User);
        var client = new CurrencyEntryHttpClient(Client);

        // act
        var result = await client.GetEntry(_testAccountId, entryId);

        // assert
        Assert.NotNull(result);
        Assert.Equal(_testAccountId, result.AccountId);
        Assert.Equal(entryId, result.EntryId);
        Assert.Equal(postingDate, result.PostingDate);
        Assert.Equal(value, result.Value);
        Assert.Equal(valueChange, result.ValueChange);
    }

    [Fact]
    public async Task GetEntry_ForOtherUsersAccount_ReturnsForbidden()
    {
        await SeedAccount();
        await SeedEntry(1, DateTime.UtcNow.Date.AddDays(-5), 100m, 50m);
        Authorize("otheruser", _testUserId + 1, UserRole.User);

        var response = await Client.GetAsync($"api/CurrencyEntry?accountId={_testAccountId}&entryId=1", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetYoungestEntryDate_ReturnsYoungestDate_WhenEntriesExist()
    {
        // arrange
        await SeedAccount();
        var oldDate = DateTime.UtcNow.Date.AddDays(-10);
        var youngDate = DateTime.UtcNow.Date.AddDays(-1);
        await SeedEntry(1, oldDate, 100m, 50m);
        await SeedEntry(2, youngDate, 200m, 100m);
        Authorize("user", _testUserId, UserRole.User);
        var client = new CurrencyEntryHttpClient(Client);

        // act
        var result = await client.GetYoungestEntryDate(_testAccountId);

        // assert
        Assert.NotNull(result);
        Assert.Equal(youngDate, result);
    }

    [Fact]
    public async Task GetOldestEntryDate_ReturnsOldestDate_WhenEntriesExist()
    {
        // arrange
        await SeedAccount();
        var oldDate = DateTime.UtcNow.Date.AddDays(-20);
        var recentDate = DateTime.UtcNow.Date.AddDays(-5);
        await SeedEntry(1, oldDate, 100m, 50m);
        await SeedEntry(2, recentDate, 200m, 100m);
        Authorize("user", _testUserId, UserRole.User);
        var client = new CurrencyEntryHttpClient(Client);

        // act
        var result = await client.GetOldestEntryDate(_testAccountId);

        // assert
        Assert.NotNull(result);
        Assert.Equal(oldDate, result);
    }

    public override void Dispose()
    {
        base.Dispose();
        _testDatabase?.Dispose();
        _testDatabase = null;
        GC.SuppressFinalize(this);
    }
}