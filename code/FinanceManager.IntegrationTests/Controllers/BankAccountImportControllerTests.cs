using FinanceManager.Application.Services;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace FinanceManager.IntegrationTests.Controllers;

[Collection("api")]
public class BankAccountImportControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private const int _testUserId = 77;
    private const int _testAccountId = 123;
    private TestDatabase? _testDatabase;
    private readonly DateTime _utcNow = DateTime.UtcNow;
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
        planVerifierMock.Setup(x => x.GetUsedRecordsCapacity(_testUserId)).ReturnsAsync(0);
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
            Name = "Test Bank Account",
            AccountLabel = AccountLabel.Other,
            AccountType = AccountType.Currency
        });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedExistingEntryExactMatch(DateTime postingDate, decimal valueChange)
    {
        if (_testDatabase is null) return;
        _testDatabase.Context.BankEntries.Add(new BankAccountEntry(_testAccountId, 0, postingDate, valueChange, valueChange)
        {
            Description = string.Empty,
            Labels = []
        });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ImportBankEntries_TwoImportZeroExisting_NoConflicts()
    {
        // arrange
        await SeedAccount();
        Authorize("user", _testUserId, UserRole.User);

        List<CurrencyEntryImportRecordDto> entries =
        [
            new(_utcNow.Date.AddDays(-2).AddHours(10), 100m),
            new(_utcNow.Date.AddDays(-1).AddHours(9), -50m)
        ];

        // act
        var result = await new BankAccountImportHttpClient(Client).ImportBankEntriesAsync(new(_testAccountId, entries));

        // assert
        Assert.NotNull(result);
        Assert.Equal(_testAccountId, result!.AccountId);
        Assert.Equal(entries.Count, result.Imported);
        Assert.Equal(0, result.Failed);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public async Task ImportBankEntries_WithExactMatch_ReturnsConflict()
    {
        // arrange
        await SeedAccount();
        Authorize("user", _testUserId, UserRole.User);
        var client = new BankAccountImportHttpClient(Client);
        var conflictDate = _utcNow.Date.AddDays(-3).AddHours(12);
        var conflictValue = 25m;
        await SeedExistingEntryExactMatch(conflictDate, conflictValue);
        List<CurrencyEntryImportRecordDto> entries =
        [
            new(conflictDate, conflictValue)
        ];
        var dto = new BankDataImportDto(_testAccountId, entries);

        // act
        var result = await client.ImportBankEntriesAsync(dto);

        // assert
        Assert.NotNull(result);
        Assert.Equal(0, result!.Imported); // exact match day skipped for import
        Assert.Equal(0, result.Failed);
        Assert.Single(result.Conflicts);
        var conflict = result.Conflicts.First();
        Assert.True(conflict.IsExactMatch);
        Assert.Equal(conflictDate, conflict.DateTime);
    }

    [Fact]
    public async Task ImportBankEntries_OneExactMatchOneExistingConflict_ReturnsConflicts()
    {
        // arrange
        await SeedAccount();
        Authorize("user", _testUserId, UserRole.User);
        var client = new BankAccountImportHttpClient(Client);
        var conflictDate = _utcNow.Date.AddDays(-3).AddHours(12);
        var conflictValue = 25m;
        await SeedExistingEntryExactMatch(conflictDate, conflictValue);
        await SeedExistingEntryExactMatch(conflictDate, conflictValue);
        List<CurrencyEntryImportRecordDto> entries = [new(conflictDate, conflictValue)];

        // act
        var result = await client.ImportBankEntriesAsync(new(_testAccountId, entries));

        // assert
        Assert.NotNull(result);
        Assert.Equal(0, result!.Imported); // exact match day skipped for import
        Assert.Equal(0, result.Failed);
        Assert.Equal(2, result.Conflicts.Count);
        var conflict = result.Conflicts.First();
        Assert.Equal(1, result.Conflicts.Count(x => x.IsExactMatch));
        Assert.Equal(conflictDate, conflict.DateTime);
    }

    [Fact]
    public async Task ImportBankEntries_OneExactMatchOneImportConflict_ReturnsConflicts()
    {
        // arrange
        await SeedAccount();
        Authorize("user", _testUserId, UserRole.User);
        var client = new BankAccountImportHttpClient(Client);
        var conflictDate = _utcNow.Date.AddDays(-3).AddHours(12);
        var conflictValue = 25m;
        await SeedExistingEntryExactMatch(conflictDate, conflictValue);
        List<CurrencyEntryImportRecordDto> entries = [new(conflictDate, conflictValue), new(conflictDate, conflictValue)];

        // act
        var result = await client.ImportBankEntriesAsync(new(_testAccountId, entries));

        // assert
        Assert.NotNull(result);
        Assert.Equal(0, result!.Imported); // exact match day skipped for import
        Assert.Equal(0, result.Failed);
        Assert.Equal(2, result.Conflicts.Count);
        var conflict = result.Conflicts.First();
        Assert.Equal(1, result.Conflicts.Count(x => x.IsExactMatch));
        Assert.Equal(conflictDate, conflict.DateTime);
    }

    [Fact]
    public async Task ResolveImportConflicts_PickingExisting_ReturnsOk()
    {
        // arrange create conflict
        await SeedAccount();
        Authorize("user", _testUserId, UserRole.User);
        var client = new BankAccountImportHttpClient(Client);
        var existingDate = _utcNow.Date.AddDays(-4).AddHours(8);
        var existingValue = 10m;
        var importValue = 5m;
        await SeedExistingEntryExactMatch(existingDate, existingValue);
        var dto = new BankDataImportDto(_testAccountId, [new(existingDate, importValue)]);
        var importResult = await client.ImportBankEntriesAsync(dto);
        Assert.NotNull(importResult);
        var existingEntry = importResult.Conflicts[1].ExistingEntry;
        Assert.NotNull(existingEntry);

        var resolution = new ResolvedImportConflict(_testAccountId, importIsPicked: false, importData: null,
        existingIsPicked: true, existingId: existingEntry!.EntryId);

        // act
        var resolveResult = await client.ResolveImportConflictsAsync([resolution]);

        // assert
        Assert.True(resolveResult);
        // ensure still exactly one entry in DB matching existing
        Assert.NotNull(_testDatabase);
        var result = await _testDatabase!.Context.BankEntries.FirstAsync(e => e.AccountId == _testAccountId && e.PostingDate == existingDate, TestContext.Current.CancellationToken);
        Assert.Equal(existingValue, result.ValueChange);
    }

    [Fact]
    public async Task ResolveImportConflicts_PickingImport_ReturnsOk()
    {
        // arrange create conflict
        await SeedAccount();
        Authorize("user", _testUserId, UserRole.User);
        var client = new BankAccountImportHttpClient(Client);
        var existingDate = _utcNow.Date.AddDays(-4).AddHours(8);
        var existingValue = 10m;
        var importValue = 5m;
        await SeedExistingEntryExactMatch(existingDate, existingValue);
        var importResult = await client.ImportBankEntriesAsync(new(_testAccountId, [new(existingDate, importValue)]));
        Assert.NotNull(importResult);
        var existingEntry = importResult.Conflicts[1].ExistingEntry;
        Assert.NotNull(existingEntry);

        var resolution = new ResolvedImportConflict(_testAccountId, importIsPicked: true, importData: new CurrencyEntryImport(existingDate, importValue),
        existingIsPicked: false, existingId: existingEntry!.EntryId);

        // act
        var resolveResult = await client.ResolveImportConflictsAsync([resolution]);

        // assert
        Assert.True(resolveResult);
        // ensure still exactly one entry in DB matching existing
        Assert.NotNull(_testDatabase);
        var result = await _testDatabase!.Context.BankEntries.SingleAsync(e => e.AccountId == _testAccountId && e.PostingDate == existingDate, TestContext.Current.CancellationToken);
        Assert.Equal(importValue, result.ValueChange);
    }

    public override void Dispose()
    {
        base.Dispose();
        _testDatabase?.Dispose();
        _testDatabase = null;
        GC.SuppressFinalize(this);
    }
}