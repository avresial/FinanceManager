using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Bond.Commands;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Commands;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using FinanceManager.Tests.Integration.Support.LegacyAccountClients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.FinancialAccounts.Bond.Controllers;

[Trait("Category", "Integration")]
public class BondEntryControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private const int _testUserId = 88;
    private const int _testAccountId = 789;
    private TestDatabase? _testDatabase;

    protected override void ConfigureServices(IServiceCollection services)
    {
        // Replace DbContext with in-memory test context
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        _testDatabase = new TestDatabase();
        services.AddSingleton(_testDatabase.Context);

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
            Name = "Test Bond Account",
            AccountLabel = AccountLabel.Other,
            AccountType = AccountType.Bond
        });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedEntry(long entryId, DateTime postingDate, decimal value, decimal valueChange, int bondDetailsId = 1)
    {
        if (_testDatabase is null) return;
        _testDatabase.Context.BondEntries.Add(new BondAccountEntry(_testAccountId, entryId, postingDate, value, valueChange, bondDetailsId));
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetEntry_ReturnsEntry_WhenExists()
    {
        // arrange
        await SeedAccount();
        var entryId = 1;
        var postingDate = DateTime.UtcNow.Date.AddDays(-5);
        var value = 1000m;
        var valueChange = 50m;
        var bondDetailsId = 101;
        await SeedEntry(entryId, postingDate, value, valueChange, bondDetailsId);
        Authorize("user", _testUserId, UserRole.User);

        // act
        var result = await Client.GetFromJsonAsync<BondAccountEntry>($"api/BondEntry?accountId={_testAccountId}&entryId={entryId}", TestContext.Current.CancellationToken);

        // assert
        Assert.NotNull(result);
        Assert.Equal(_testAccountId, result.AccountId);
        Assert.Equal(entryId, result.EntryId);
        Assert.Equal(postingDate, result.PostingDate);
        Assert.Equal(value, result.Value);
        Assert.Equal(valueChange, result.ValueChange);
        Assert.Equal(bondDetailsId, result.BondDetailsId);
    }

    [Fact]
    public async Task GetEntry_ForOtherUsersAccount_ReturnsForbidden()
    {
        await SeedAccount();
        await SeedEntry(1, DateTime.UtcNow.Date.AddDays(-5), 1000m, 50m, 101);
        Authorize("otheruser", _testUserId + 1, UserRole.User);

        var response = await Client.GetAsync($"api/BondEntry?accountId={_testAccountId}&entryId=1", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetYoungestEntryDate_ReturnsYoungestDate_WhenEntriesExist()
    {
        // arrange
        await SeedAccount();
        var oldDate = DateTime.UtcNow.Date.AddDays(-10);
        var youngDate = DateTime.UtcNow.Date.AddDays(-1);
        await SeedEntry(1, oldDate, 1000m, 50m);
        await SeedEntry(2, youngDate, 2000m, 100m);
        Authorize("user", _testUserId, UserRole.User);
        var client = new BondEntryHttpClient(Client);

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
        await SeedEntry(1, oldDate, 1000m, 50m);
        await SeedEntry(2, recentDate, 2000m, 100m);
        Authorize("user", _testUserId, UserRole.User);
        var client = new BondEntryHttpClient(Client);

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