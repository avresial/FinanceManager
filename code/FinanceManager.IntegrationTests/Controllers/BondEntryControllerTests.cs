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
        Assert.Equal(250m, dbEntry.ValueChange);
        Assert.Equal(250m, dbEntry.Value);
    }
    [Fact]
    public async Task AddMultipleEntries_WithDifferentBonds_CalculatesValuesCorrectly()
    {
        // arrange
        await SeedAccount();
        Authorize("user", _testUserId, UserRole.User);
        var client = new BondEntryHttpClient(Client);

        // Add first entry for Bond A (bondDetailsId = 100)
        var bondAEntry = new AddBondAccountEntry(
            _testAccountId,
            1,
            DateTime.UtcNow.Date.AddDays(-10),
            2000m, // value (should be ignored and recalculated)
            1000m, // valueChange
            100    // bondDetailsId for Bond A
        );

        // act - add first entry for Bond A
        var result1 = await client.AddEntryAsync(bondAEntry);

        // assert first entry was added
        Assert.True(result1);

        // verify Bond A entry in database
        var dbEntryA = await _testDatabase!.Context.BondEntries
            .FirstOrDefaultAsync(e => e.AccountId == _testAccountId && e.BondDetailsId == 100,
                                 TestContext.Current.CancellationToken);
        Assert.NotNull(dbEntryA);
        Assert.Equal(1000m, dbEntryA.ValueChange);
        Assert.Equal(1000m, dbEntryA.Value); // First entry: Value should equal ValueChange

        // Add second entry for Bond B (bondDetailsId = 200) with different posting date
        var bondBEntry = new AddBondAccountEntry(
            _testAccountId,
            2,
            DateTime.UtcNow.Date.AddDays(-5),
            1000m,  // value (should be ignored and recalculated)
            500m,  // valueChange
            200    // bondDetailsId for Bond B (different bond!)
        );

        // act - add second entry for Bond B
        var result2 = await client.AddEntryAsync(bondBEntry);

        // assert second entry was added
        Assert.True(result2);

        // verify Bond B entry in database
        var dbEntryB = await _testDatabase!.Context.BondEntries
            .FirstOrDefaultAsync(e => e.AccountId == _testAccountId && e.BondDetailsId == 200,
                                 TestContext.Current.CancellationToken);
        Assert.NotNull(dbEntryB);
        Assert.Equal(500m, dbEntryB.ValueChange);
        Assert.Equal(500m, dbEntryB.Value);

        // Verify Bond A value wasn't affected
        dbEntryA = await _testDatabase!.Context.BondEntries
            .FirstOrDefaultAsync(e => e.AccountId == _testAccountId && e.BondDetailsId == 100,
                                 TestContext.Current.CancellationToken);
        Assert.NotNull(dbEntryA);
        Assert.Equal(1000m, dbEntryA.Value); // Should still be 1000
        Assert.Equal(1000m, dbEntryA.ValueChange); // Should still be 1000
    }

    [Fact]
    public async Task AddMultipleEntries_ForSameBond_CalculatesValuesCorrectly()
    {
        // arrange
        await SeedAccount();
        Authorize("user", _testUserId, UserRole.User);
        var client = new BondEntryHttpClient(Client);

        // Add first entry for Bond A
        var bondAEntry1 = new AddBondAccountEntry(
            _testAccountId,
            1,
            DateTime.UtcNow.Date.AddDays(-10),
            1000m,
            1000m, // Initial investment
            100
        );

        // act - add first entry
        var result1 = await client.AddEntryAsync(bondAEntry1);
        Assert.True(result1);

        // Add second entry for the SAME Bond A (should accumulate)
        var bondAEntry2 = new AddBondAccountEntry(
            _testAccountId,
            2,
            DateTime.UtcNow.Date.AddDays(-5),
            1500m,
            50m,   // Value change (interest or additional investment)
            100    // Same bondDetailsId
        );

        // act - add second entry for same bond
        var result2 = await client.AddEntryAsync(bondAEntry2);
        Assert.True(result2);

        // verify both entries in database
        var entries = await _testDatabase!.Context.BondEntries
            .Where(e => e.AccountId == _testAccountId && e.BondDetailsId == 100)
            .OrderBy(e => e.PostingDate)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, entries.Count);

        // First entry should have value = valueChange (1000)
        Assert.Equal(1000m, entries[0].Value);
        Assert.Equal(1000m, entries[0].ValueChange);

        // Second entry should accumulate: previous value + valueChange = 1000 + 50 = 1050
        Assert.Equal(1050m, entries[1].Value);
        Assert.Equal(50m, entries[1].ValueChange);
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