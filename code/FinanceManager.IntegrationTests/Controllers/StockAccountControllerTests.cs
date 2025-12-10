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
public class StockAccountControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private const int _testUserId = 89;
    private const int _testAccountId = 789;
    private const string _testAccountName = "Test Stock Account";
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

        var entry1 = new StockAccountEntry(_testAccountId, 1, DateTime.UtcNow.Date.AddDays(-10), 10000m, 10000m, "AAPL", InvestmentType.Stock);
        var entry2 = new StockAccountEntry(_testAccountId, 2, DateTime.UtcNow.Date.AddDays(-5), 10500m, 500m, "AAPL", InvestmentType.Stock);
        var entry3 = new StockAccountEntry(_testAccountId, 3, DateTime.UtcNow.Date.AddDays(-2), 11000m, 500m, "AAPL", InvestmentType.Stock);

        _testDatabase.Context.StockEntries.AddRange(entry1, entry2, entry3);
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Get_ReturnsAllAccountsForUser()
    {
        // arrange
        await SeedAccount();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new StockAccountHttpClient(Client);

        // act
        var accounts = await client.GetAvailableAccountsAsync();

        // assert
        var accountList = accounts.ToList();
        Assert.NotEmpty(accountList);
        Assert.Contains(accountList, a => a.AccountId == _testAccountId && a.AccountName == _testAccountName);
    }

    [Fact]
    public async Task GetById_ReturnsAccount()
    {
        // arrange
        await SeedAccount();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new StockAccountHttpClient(Client);

        // act
        var account = await client.GetAccountAsync(_testAccountId);

        // assert
        Assert.NotNull(account);
        Assert.Equal(_testAccountId, account!.AccountId);
        Assert.Equal(_testUserId, account.UserId);
        Assert.Equal(_testAccountName, account.Name);
    }

    [Fact]
    public async Task GetWithDateRange_ReturnsAccountWithEntries()
    {
        // arrange
        await SeedAccountWithEntries();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new StockAccountHttpClient(Client);
        var startDate = DateTime.UtcNow.Date.AddDays(-15);
        var endDate = DateTime.UtcNow.Date;

        // act
        var account = await client.GetAccountWithEntriesAsync(_testAccountId, startDate, endDate);

        // assert
        Assert.NotNull(account);
        Assert.Equal(_testAccountId, account!.AccountId);
        Assert.NotEmpty(account.Entries);
        Assert.Equal(3, account.Entries.Count);
        Assert.Contains(account.Entries, e => e.Ticker == "AAPL");
    }

    [Fact]
    public async Task Add_CreatesNewAccount()
    {
        // arrange
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new StockAccountHttpClient(Client);
        var newAccountCmd = new AddAccount("New Investment Account");

        // act
        var newAccountId = await client.AddAccountAsync(newAccountCmd);

        // assert
        Assert.NotNull(newAccountId);
        Assert.True(newAccountId > 0);

        // verify in database
        Assert.NotNull(_testDatabase);
        var accountInDb = await _testDatabase!.Context.Accounts
            .FirstOrDefaultAsync(a => a.AccountId == newAccountId.Value, TestContext.Current.CancellationToken);
        Assert.NotNull(accountInDb);
        Assert.Equal("New Investment Account", accountInDb!.Name);
        Assert.Equal(_testUserId, accountInDb.UserId);
    }

    [Fact]
    public async Task Update_ModifiesAccountName()
    {
        // arrange
        await SeedAccount();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new StockAccountHttpClient(Client);
        var updatedName = "Updated Stock Account Name";
        var updateCmd = new UpdateAccount(_testAccountId, updatedName, AccountLabel.Stock);

        // act
        var result = await client.UpdateAccountAsync(updateCmd);

        // assert
        Assert.True(result);

        // verify in database
        Assert.NotNull(_testDatabase);
        var accountInDb = await _testDatabase!.Context.Accounts
            .FirstOrDefaultAsync(a => a.AccountId == _testAccountId, TestContext.Current.CancellationToken);
        Assert.NotNull(accountInDb);
        Assert.Equal(updatedName, accountInDb!.Name);
    }

    [Fact]
    public async Task Delete_RemovesAccountAndEntries()
    {
        // arrange
        await SeedAccountWithEntries();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new StockAccountHttpClient(Client);

        // act
        var result = await client.DeleteAccountAsync(_testAccountId);

        // assert
        Assert.True(result);

        // verify account removed from database
        Assert.NotNull(_testDatabase);
        var accountInDb = await _testDatabase!.Context.Accounts
            .FirstOrDefaultAsync(a => a.AccountId == _testAccountId, TestContext.Current.CancellationToken);
        Assert.Null(accountInDb);

        // verify entries removed from database
        var entriesInDb = await _testDatabase.Context.StockEntries
            .Where(e => e.AccountId == _testAccountId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(entriesInDb);
    }

    public override void Dispose()
    {
        base.Dispose();
        _testDatabase?.Dispose();
        _testDatabase = null;
        GC.SuppressFinalize(this);
    }
}