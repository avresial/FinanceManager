using FinanceManager.Application.Commands.Account;
using FinanceManager.Application.Services;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace FinanceManager.IntegrationTests.Controllers;

[Collection("api")]
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
        planVerifierMock.Setup(x => x.CanAddMoreAccounts(_testUserId)).ReturnsAsync(true);
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

    private async Task SeedEntry(int entryId, DateTime postingDate, decimal value, decimal valueChange, int bondDetailsId = 1)
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
        var client = new BondEntryHttpClient(Client);

        // act
        var result = await client.GetEntry(_testAccountId, entryId);

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

    [Fact]
    public async Task AddEntry_AddsEntry_Successfully()
    {
        // arrange
        await SeedAccount();
        Authorize("user", _testUserId, UserRole.User);
        var client = new BondEntryHttpClient(Client);
        var addEntry = new AddBondAccountEntry(
            _testAccountId,
            100,
            DateTime.UtcNow.Date.AddDays(-3),
            5000m,
            250m,
            202
        );

        // act
        var result = await client.AddEntryAsync(addEntry);

        // assert
        Assert.True(result);

        // verify entry was added by checking it exists in the database
        var dbEntry = await _testDatabase!.Context.BondEntries
            .FirstOrDefaultAsync(e => e.AccountId == _testAccountId &&
                                     e.PostingDate == addEntry.PostingDate &&
                                     e.BondDetailsId == addEntry.BondDetailsId,
                                     TestContext.Current.CancellationToken);
        Assert.NotNull(dbEntry);
        Assert.Equal(addEntry.ValueChange, dbEntry.ValueChange);
    }

    [Fact]
    public async Task DeleteEntry_DeletesEntry_Successfully()
    {
        // arrange
        await SeedAccount();
        var entryId = 5;
        await SeedEntry(entryId, DateTime.UtcNow.Date.AddDays(-7), 3000m, 150m);
        Authorize("user", _testUserId, UserRole.User);
        var client = new BondEntryHttpClient(Client);

        // act
        var result = await client.DeleteEntryAsync(_testAccountId, entryId);

        // assert
        Assert.True(result);

        // verify entry was deleted from database
        var dbEntry = await _testDatabase!.Context.BondEntries
            .FirstOrDefaultAsync(e => e.AccountId == _testAccountId && e.EntryId == entryId, TestContext.Current.CancellationToken);
        Assert.Null(dbEntry);
    }

    [Fact]
    public async Task UpdateEntry_UpdatesEntry_Successfully()
    {
        // arrange
        await SeedAccount();
        var entryId = 7;
        var originalDate = DateTime.UtcNow.Date.AddDays(-10);
        await SeedEntry(entryId, originalDate, 4000m, 200m, 303);
        Authorize("user", _testUserId, UserRole.User);
        var client = new BondEntryHttpClient(Client);

        var entry = await client.GetEntry(_testAccountId, entryId);
        Assert.NotNull(entry);

        var updateEntry = new UpdateBondAccountEntry(
            entry.AccountId,
            entry.EntryId,
            entry.PostingDate,
            entry.Value,
            300m, // Updated ValueChange
            404 // Updated BondDetailsId
        );

        // act
        var result = await client.UpdateEntryAsync(updateEntry);

        // assert
        Assert.True(result);

        // verify entry was updated in database
        var dbEntry = await _testDatabase!.Context.BondEntries
            .FirstOrDefaultAsync(e => e.AccountId == _testAccountId && e.EntryId == entryId, TestContext.Current.CancellationToken);
        Assert.NotNull(dbEntry);
        Assert.Equal(404, dbEntry.BondDetailsId);
        Assert.Equal(300m, dbEntry.ValueChange);
    }

    public override void Dispose()
    {
        base.Dispose();
        _testDatabase?.Dispose();
        _testDatabase = null;
        GC.SuppressFinalize(this);
    }
}