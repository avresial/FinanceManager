using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Shared.Commands;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.FinancialAccounts.Investments.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class InvestmentAccountControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private const int _testUserId = 92;
    private const int _testAccountId = 792;
    private const string _testAccountName = "Test Investment Account";
    private TestDatabase? _testDatabase;

    protected override void ConfigureServices(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        _testDatabase = new TestDatabase();
        services.AddSingleton(_testDatabase.Context);

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
            AccountLabel = AccountLabel.Stock,
            AccountType = AccountType.Stock
        });
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Get_ReturnsAllAccountsForUser()
    {
        await SeedAccount();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentAccountHttpClient(Client);

        var accounts = await client.GetAvailableAccountsAsync();

        Assert.NotEmpty(accounts);
        Assert.Contains(accounts, a => a.AccountId == _testAccountId && a.AccountName == _testAccountName);
    }

    [Fact]
    public async Task GetById_ReturnsAccount()
    {
        await SeedAccount();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentAccountHttpClient(Client);

        var account = await client.GetAccountAsync(_testAccountId);

        Assert.NotNull(account);
        Assert.Equal(_testAccountId, account!.AccountId);
        Assert.Equal(_testUserId, account.UserId);
        Assert.Equal(_testAccountName, account.Name);
    }

    [Fact]
    public async Task GetById_ForOtherUsersAccount_ReturnsForbidden()
    {
        await SeedAccount();
        Authorize("otheruser", _testUserId + 1, UserRole.User);

        var response = await Client.GetAsync($"api/InvestmentAccount/{_testAccountId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Add_CreatesNewAccount()
    {
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentAccountHttpClient(Client);
        var newAccountCmd = new AddAccount("New Investment Account");

        var newAccountId = await client.AddAccountAsync(newAccountCmd);

        Assert.NotNull(newAccountId);
        Assert.True(newAccountId > 0);

        Assert.NotNull(_testDatabase);
        var accountInDb = await _testDatabase!.Context.Accounts
            .FirstOrDefaultAsync(a => a.AccountId == newAccountId!.Value, TestContext.Current.CancellationToken);
        Assert.NotNull(accountInDb);
        Assert.Equal("New Investment Account", accountInDb!.Name);
        Assert.Equal(_testUserId, accountInDb.UserId);
        Assert.Equal(AccountType.Stock, accountInDb.AccountType);
    }

    [Fact]
    public async Task Update_ModifiesAccountName()
    {
        await SeedAccount();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentAccountHttpClient(Client);
        var updatedName = "Updated Investment Account Name";
        var updateCmd = new UpdateAccount(_testAccountId, updatedName, AccountLabel.Stock);

        var result = await client.UpdateAccountAsync(updateCmd);

        Assert.True(result);

        Assert.NotNull(_testDatabase);
        var accountInDb = await _testDatabase!.Context.Accounts
            .FirstOrDefaultAsync(a => a.AccountId == _testAccountId, TestContext.Current.CancellationToken);
        Assert.NotNull(accountInDb);
        Assert.Equal(updatedName, accountInDb!.Name);
    }

    [Fact]
    public async Task Delete_RemovesAccount()
    {
        await SeedAccount();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentAccountHttpClient(Client);

        var result = await client.DeleteAccountAsync(_testAccountId);

        Assert.True(result);

        Assert.NotNull(_testDatabase);
        var accountInDb = await _testDatabase!.Context.Accounts
            .FirstOrDefaultAsync(a => a.AccountId == _testAccountId, TestContext.Current.CancellationToken);
        Assert.Null(accountInDb);
    }

    public override void Dispose()
    {
        base.Dispose();
        _testDatabase?.Dispose();
        _testDatabase = null;
        GC.SuppressFinalize(this);
    }
}