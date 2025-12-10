using FinanceManager.Application.Commands.Account;
using FinanceManager.Application.Services;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace FinanceManager.IntegrationTests.Controllers;

[Collection("api")]
public class StockEntryControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private const int _testUserId = 90;
    private const int _testAccountId = 790;
    private const string _testAccountName = "Test Stock Entry Account";
    private TestDatabase? _testDatabase;

    protected override void ConfigureServices(IServiceCollection services)
    {
        // Replace DbContext with in-memory test context
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        _testDatabase = new TestDatabase();
        services.AddSingleton(_testDatabase.Context);

        // Mock user plan verifier to allow operations
        var planVerifierMock = new Mock<IUserPlanVerifier>();
        planVerifierMock.Setup(x => x.CanAddMoreAccounts(_testUserId)).ReturnsAsync(true);
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
            Name = _testAccountName,
            AccountLabel = AccountLabel.Stock,
            AccountType = AccountType.Stock
        });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedAccountWithEntries()
    {
        await SeedAccount();
        if (_testDatabase is null) return;

        var entry1 = new StockAccountEntry(_testAccountId, 1, DateTime.UtcNow.Date.AddDays(-10), 10000m, 10000m, "MSFT", InvestmentType.Stock);
        var entry2 = new StockAccountEntry(_testAccountId, 2, DateTime.UtcNow.Date.AddDays(-5), 10500m, 500m, "MSFT", InvestmentType.Stock);
        var entry3 = new StockAccountEntry(_testAccountId, 3, DateTime.UtcNow.Date.AddDays(-2), 11000m, 500m, "MSFT", InvestmentType.Stock);

        _testDatabase.Context.StockEntries.AddRange(entry1, entry2, entry3);
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetYoungestEntryDate_ReturnsLatestDate()
    {
        // arrange
        await SeedAccountWithEntries();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new StockEntryHttpClient(Client);

        // act
        var youngestDate = await client.GetYoungestEntryDate(_testAccountId);

        // assert
        Assert.NotNull(youngestDate);
        Assert.Equal(DateTime.UtcNow.Date.AddDays(-2), youngestDate!.Value.Date);
    }

    [Fact]
    public async Task GetOldestEntryDate_ReturnsEarliestDate()
    {
        // arrange
        await SeedAccountWithEntries();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new StockEntryHttpClient(Client);

        // act
        var oldestDate = await client.GetOldestEntryDate(_testAccountId);

        // assert
        Assert.NotNull(oldestDate);
        Assert.Equal(DateTime.UtcNow.Date.AddDays(-10), oldestDate!.Value.Date);
    }

    [Fact]
    public async Task Add_CreatesNewEntry()
    {
        // arrange
        await SeedAccount();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new StockEntryHttpClient(Client);
        var newEntry = new StockAccountEntry(_testAccountId, 0, DateTime.UtcNow.Date, 5000m, 5000m, "GOOGL", InvestmentType.Stock);
        var addCmd = new AddStockAccountEntry(newEntry);

        // act
        var result = await client.AddEntryAsync(addCmd);

        // assert
        Assert.True(result);

        // verify in database
        Assert.NotNull(_testDatabase);
        var entryInDb = await _testDatabase!.Context.StockEntries
            .Where(e => e.AccountId == _testAccountId && e.Ticker == "GOOGL")
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(entryInDb);
        Assert.Equal(5000m, entryInDb!.Value);
        Assert.Equal(5000m, entryInDb.ValueChange);
    }

    [Fact]
    public async Task Update_ModifiesEntry()
    {
        // arrange
        await SeedAccountWithEntries();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new StockEntryHttpClient(Client);
        var updateCmd = new UpdateStockAccountEntry(
            _testAccountId,
            2,
            DateTime.UtcNow.Date.AddDays(-5),
            11500m,
            1500m,
            "MSFT",
            InvestmentType.Stock,
            []
        );

        // act
        var result = await client.UpdateEntryAsync(updateCmd);

        // assert
        Assert.True(result);

        // verify in database
        Assert.NotNull(_testDatabase);
        var entryInDb = await _testDatabase!.Context.StockEntries
            .FirstOrDefaultAsync(e => e.AccountId == _testAccountId && e.EntryId == 2, TestContext.Current.CancellationToken);
        Assert.NotNull(entryInDb);
        Assert.Equal(11500m, entryInDb!.Value);
        Assert.Equal(1500m, entryInDb.ValueChange);
    }

    [Fact]
    public async Task Delete_RemovesEntry()
    {
        // arrange
        await SeedAccountWithEntries();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new StockEntryHttpClient(Client);

        // act
        var result = await client.DeleteEntryAsync(_testAccountId, 2);

        // assert
        Assert.True(result);

        // verify entry removed from database
        Assert.NotNull(_testDatabase);
        var entryInDb = await _testDatabase!.Context.StockEntries
            .FirstOrDefaultAsync(e => e.AccountId == _testAccountId && e.EntryId == 2, TestContext.Current.CancellationToken);
        Assert.Null(entryInDb);

        // verify other entries remain
        var remainingEntries = await _testDatabase.Context.StockEntries
            .Where(e => e.AccountId == _testAccountId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, remainingEntries.Count);
    }

    public override void Dispose()
    {
        base.Dispose();
        _testDatabase?.Dispose();
        _testDatabase = null;
        GC.SuppressFinalize(this);
    }
}