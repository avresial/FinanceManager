using FinanceManager.Application.Commands.Account;
using FinanceManager.Application.Services;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Commands.Account;
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
public class BondAccountControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private const int _testUserId = 91;
    private const int _testAccountId = 791;
    private const string _testAccountName = "Test Bond Account";
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
        var db = _testDatabase;
        if (db is null) return;
        if (await db.Context.Accounts.AnyAsync(a => a.AccountId == _testAccountId, TestContext.Current.CancellationToken)) return;
        db.Context.Accounts.Add(new FinancialAccountBaseDto
        {
            AccountId = _testAccountId,
            UserId = _testUserId,
            Name = _testAccountName,
            AccountLabel = AccountLabel.Other,
            AccountType = AccountType.Bond
        });
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedAccountWithEntries()
    {
        await SeedAccount();
        var db = _testDatabase;
        if (db is null) return;

        var entry1 = new BondAccountEntry(_testAccountId, 1, DateTime.UtcNow.Date.AddDays(-10), 10000m, 10000m, 101);
        var entry2 = new BondAccountEntry(_testAccountId, 2, DateTime.UtcNow.Date.AddDays(-5), 10500m, 500m, 101);
        var entry3 = new BondAccountEntry(_testAccountId, 3, DateTime.UtcNow.Date.AddDays(-2), 11000m, 500m, 101);

        db.Context.BondEntries.AddRange(entry1, entry2, entry3);
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Get_ReturnsAllAccountsForUser()
    {
        // arrange
        await SeedAccount();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new BondAccountHttpClient(Client);

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
        var client = new BondAccountHttpClient(Client);

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
        var client = new BondAccountHttpClient(Client);
        var startDate = DateTime.UtcNow.Date.AddDays(-15);
        var endDate = DateTime.UtcNow.Date;

        // act
        var account = await client.GetAccountWithEntriesAsync(_testAccountId, startDate, endDate);

        // assert
        Assert.NotNull(account);
        Assert.Equal(_testAccountId, account!.AccountId);
        Assert.NotEmpty(account.Entries);
        Assert.Equal(3, account.Entries.Count);
        Assert.Contains(account.Entries, e => e.BondDetailsId == 101);
    }

    [Fact]
    public async Task Add_CreatesNewAccount()
    {
        // arrange
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new BondAccountHttpClient(Client);
        var newAccountCmd = new AddAccount("New Bond Account");

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
        Assert.Equal("New Bond Account", accountInDb!.Name);
        Assert.Equal(_testUserId, accountInDb.UserId);
    }

    [Fact]
    public async Task Update_ModifiesAccountName()
    {
        // arrange
        await SeedAccount();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new BondAccountHttpClient(Client);
        var updatedName = "Updated Bond Account Name";
        var updateCmd = new UpdateAccount(_testAccountId, updatedName, AccountLabel.Other);

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
        var client = new BondAccountHttpClient(Client);

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
        var entriesInDb = await _testDatabase.Context.BondEntries
            .Where(e => e.AccountId == _testAccountId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(entriesInDb);
    }

    [Fact]
    public async Task ExportCsv_ReturnsCsvFile()
    {
        await SeedAccountWithEntries();
        Authorize("testuser", _testUserId, UserRole.User);

        var startDate = DateTime.UtcNow.Date.AddDays(-15);
        var endDate = DateTime.UtcNow.Date;
        var url = $"{Client.BaseAddress}api/BondAccount/export/{_testAccountId}?startDate={Uri.EscapeDataString(startDate.ToString("O"))}&endDate={Uri.EscapeDataString(endDate.ToString("O"))}";

        var response = await Client.GetAsync(url, TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

        var csv = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("PostingDate", csv, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BondDetailsId", csv, StringComparison.OrdinalIgnoreCase);
    }

    public override void Dispose()
    {
        base.Dispose();
        _testDatabase?.Dispose();
        _testDatabase = null;
        GC.SuppressFinalize(this);
    }
}