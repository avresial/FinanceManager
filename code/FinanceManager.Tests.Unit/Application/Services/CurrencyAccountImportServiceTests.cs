using FinanceManager.Application.FinancialAccounts.Currencies.Import;
using FinanceManager.Application.FinancialAccounts.Shared.Imports;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Application.TransactionRules;
using FinanceManager.Application.TransactionRules.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Imports;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class CurrencyAccountImportServiceTests
{
    private readonly Mock<IAccountEntryRepository<CurrencyAccountEntry>> _mockAccountEntryRepository;
    private readonly Mock<IInvestmentTransactionRepository> _mockInvestmentTransactionRepository;
    private readonly Mock<IAccountEntryRepository<BondAccountEntry>> _mockBondEntryRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly UserPlanVerifier _userPlanVerifier;
    private readonly CurrencyAccountImportService _service;

    public CurrencyAccountImportServiceTests()
    {
        _mockAccountEntryRepository = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        _mockInvestmentTransactionRepository = new Mock<IInvestmentTransactionRepository>();
        _mockBondEntryRepository = new Mock<IAccountEntryRepository<BondAccountEntry>>();

        _mockUserRepository = new Mock<IUserRepository>();
        var user = new User() { Login = "TestUser", UserId = 1, PricingLevel = PricingLevel.Premium, CreationDate = DateTime.UtcNow };
        _mockUserRepository.Setup(x => x.GetUser(It.IsAny<int>())).ReturnsAsync(user);

        // create a real UserPlanVerifier so its CanAddMoreEntries method can be used in test
        _userPlanVerifier = new UserPlanVerifier(_mockAccountEntryRepository.Object,
            _mockInvestmentTransactionRepository.Object, _mockBondEntryRepository.Object, _mockUserRepository.Object);

        // Default: users have no recorded entries of any type, so plan checks pass unless a test overrides this.
        _mockAccountEntryRepository.Setup(x => x.GetEntriesCountPerUser(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>());
        _mockInvestmentTransactionRepository.Setup(x => x.GetCountPerUser(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>());
        _mockBondEntryRepository.Setup(x => x.GetEntriesCountPerUser(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>());

        _mockAccountEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<CurrencyAccountEntry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mockLogger = new Mock<ILogger<CurrencyAccountImportService>>();
        var importAccountValidator = new ImportAccountValidator(_userPlanVerifier);
        _service = new CurrencyAccountImportService(_mockAccountEntryRepository.Object, importAccountValidator, mockLogger.Object);
    }

    private const int _owner = 7;
    private const int _currency = 3;

    private CurrencyAccountImportService OwnerService(Mock<ITransactionRuleService>? rules = null) => new(
        _mockAccountEntryRepository.Object, new ImportAccountValidator(_userPlanVerifier),
        new Mock<ILogger<CurrencyAccountImportService>>().Object, rules?.Object);

    private void SetupOwnerRows(params CurrencyAccountEntry[] rows) =>
        _mockAccountEntryRepository.Setup(x => x.GetByOwner(_owner, _currency, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(rows.ToAsyncEnumerable());

    [Fact]
    public async Task ImportOwnerEntries_ProbesTheOwnersPoolOfTheCurrency_StoresUnlinkedRows_AndNeverRecalculates()
    {
        var date = new DateTime(2025, 3, 4, 10, 0, 0, DateTimeKind.Utc);
        SetupOwnerRows();
        List<CurrencyAccountEntry>? added = null;
        _mockAccountEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<CurrencyAccountEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<CurrencyAccountEntry> entries, CancellationToken _) => added = [.. entries]).ReturnsAsync(true);

        var result = await OwnerService().ImportOwnerEntries(_owner, _currency, [new CurrencyEntryImport(date, -5m, "Shop", "Coffee")], TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Imported);
        var entry = Assert.Single(added!);
        Assert.Null(entry.AccountId);
        Assert.Equal(_owner, entry.UserId);
        Assert.Equal(_currency, entry.CurrencyId);
        Assert.Equal(0m, entry.Value);
        Assert.Equal(-5m, entry.ValueChange);
        // The whole last UTC day is probed, so rows late in the day are seen.
        _mockAccountEntryRepository.Verify(x => x.GetByOwner(_owner, _currency, date.Date, date.Date.AddDays(1).AddTicks(-1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportOwnerEntries_UnrelatedRowsOnTheSameDay_DoNotBlockTheImport()
    {
        var date = new DateTime(2025, 3, 4, 10, 0, 0, DateTimeKind.Utc);
        SetupOwnerRows(new CurrencyAccountEntry(null, 1, date, 0, 10m) { UserId = _owner, CurrencyId = _currency });

        var result = await OwnerService().ImportOwnerEntries(_owner, _currency, [new CurrencyEntryImport(date, 5m)], TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Imported);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public async Task ImportOwnerEntries_ExactMatchCountsAsPresent_AndOnlyTheIncomingSurplusIsOffered()
    {
        var date = new DateTime(2025, 3, 4, 10, 0, 0, DateTimeKind.Utc);
        // One linked and one unlinked row already exist for the same key; one unrelated row must not be offered back.
        SetupOwnerRows(
            new CurrencyAccountEntry(9, 1, date, 10m, 10m) { UserId = _owner, CurrencyId = _currency },
            new CurrencyAccountEntry(null, 2, date, 0, 10m) { UserId = _owner, CurrencyId = _currency },
            new CurrencyAccountEntry(null, 3, date, 0, 99m) { UserId = _owner, CurrencyId = _currency });
        CurrencyEntryImport[] incoming =
        [
            new(date, 10m, "A", "first"),
            new(date, 10m, "B", "second"),
            new(date, 10m, "C", "third"),
        ];

        var result = await OwnerService().ImportOwnerEntries(_owner, _currency, incoming, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Imported);
        Assert.Equal(2, result.Conflicts.Count(x => x.IsExactMatch));
        var surplus = Assert.Single(result.Conflicts, x => !x.IsExactMatch);
        Assert.Equal("third", surplus.ImportEntry!.Description);
        Assert.Null(surplus.ExistingEntry);
        Assert.DoesNotContain(result.Conflicts, x => x.ExistingEntry?.EntryId == 3);
    }

    [Fact]
    public async Task ImportOwnerEntries_AppliesTheRulesLoadedOnceForTheWholeBatch()
    {
        SetupOwnerRows();
        var rules = new Mock<ITransactionRuleService>();
        rules.Setup(x => x.LoadApplicationAsync(_owner, It.IsAny<CancellationToken>())).ReturnsAsync(new TransactionRuleApplication([], []));
        var date = new DateTime(2025, 3, 4, 10, 0, 0, DateTimeKind.Utc);

        var result = await OwnerService(rules).ImportOwnerEntries(_owner, _currency,
            [new CurrencyEntryImport(date, 1m), new CurrencyEntryImport(date.AddDays(1), 2m), new CurrencyEntryImport(date.AddDays(2), 3m)], TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Imported);
        rules.Verify(x => x.LoadApplicationAsync(_owner, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportOwnerEntries_ExceedingThePlanLimit_Throws()
    {
        _mockUserRepository.Setup(x => x.GetUser(It.IsAny<int>())).ReturnsAsync(new User { Login = "Free", UserId = _owner, PricingLevel = PricingLevel.Free, CreationDate = DateTime.UtcNow });
        _mockAccountEntryRepository.Setup(x => x.GetEntriesCountPerUser(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [_owner] = int.MaxValue / 2 });

        await Assert.ThrowsAsync<InvalidOperationException>(() => OwnerService().ImportOwnerEntries(_owner, _currency,
            [new CurrencyEntryImport(DateTime.UtcNow, 1m)], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ApplyOwnerConflict_AddsTheRuleAppliedUnlinkedRow_AndReportsAFailedWrite()
    {
        var rules = new Mock<ITransactionRuleService>();
        rules.Setup(x => x.LoadApplicationAsync(_owner, It.IsAny<CancellationToken>())).ReturnsAsync(new TransactionRuleApplication([], []));
        var service = OwnerService(rules);
        var date = new DateTime(2025, 3, 4, 10, 0, 0, DateTimeKind.Utc);
        _mockAccountEntryRepository.Setup(x => x.Add(It.IsAny<CurrencyAccountEntry>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        Assert.True(await service.ApplyOwnerConflict(_owner, _currency, new CurrencyEntryImport(date, 4m, "Shop", "Surplus"), TestContext.Current.CancellationToken));

        _mockAccountEntryRepository.Verify(x => x.Add(
            It.Is<CurrencyAccountEntry>(e => e.AccountId == null && e.UserId == _owner && e.CurrencyId == _currency && e.Value == 0 && e.ValueChange == 4m
                && e.Description == "Surplus" && e.ContractorDetails == "Shop"), It.IsAny<CancellationToken>()), Times.Once);

        _mockAccountEntryRepository.Setup(x => x.Add(It.IsAny<CurrencyAccountEntry>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        Assert.False(await service.ApplyOwnerConflict(_owner, _currency, new CurrencyEntryImport(date, 4m), TestContext.Current.CancellationToken));

        _mockAccountEntryRepository.Setup(x => x.Add(It.IsAny<CurrencyAccountEntry>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        Assert.False(await service.ApplyOwnerConflict(_owner, _currency, new CurrencyEntryImport(date, 4m), TestContext.Current.CancellationToken));
        Assert.False(await service.ApplyOwnerConflict(_owner, _currency, new CurrencyEntryImport(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), 4m), TestContext.Current.CancellationToken));
    }
}