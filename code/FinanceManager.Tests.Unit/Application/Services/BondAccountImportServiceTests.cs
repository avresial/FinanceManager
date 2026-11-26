using FinanceManager.Application.FinancialAccounts.Bond.Import;
using FinanceManager.Application.FinancialAccounts.Shared.Imports;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Imports;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class BondAccountImportServiceTests
{
    private readonly Mock<IAccountEntryRepository<BondAccountEntry>> _mockBondEntryRepository;
    private readonly Mock<IAccountEntryRepository<CurrencyAccountEntry>> _mockCurrencyEntryRepository;
    private readonly Mock<IInvestmentTransactionRepository> _mockInvestmentTransactionRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly UserPlanVerifier _userPlanVerifier;
    private readonly BondAccountImportService _service;

    public BondAccountImportServiceTests()
    {
        _mockBondEntryRepository = new Mock<IAccountEntryRepository<BondAccountEntry>>();
        _mockCurrencyEntryRepository = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        _mockInvestmentTransactionRepository = new Mock<IInvestmentTransactionRepository>();
        _mockUserRepository = new Mock<IUserRepository>();

        var user = new User { Login = "TestUser", UserId = 1, PricingLevel = PricingLevel.Premium, CreationDate = DateTime.UtcNow };
        _mockUserRepository.Setup(x => x.GetUser(It.IsAny<int>())).ReturnsAsync(user);

        _userPlanVerifier = new UserPlanVerifier(
            _mockCurrencyEntryRepository.Object,
            _mockInvestmentTransactionRepository.Object,
            _mockBondEntryRepository.Object,
            _mockUserRepository.Object);

        _mockCurrencyEntryRepository.Setup(x => x.GetEntriesCountPerUser(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>());
        _mockBondEntryRepository.Setup(x => x.GetEntriesCountPerUser(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>());
        _mockInvestmentTransactionRepository.Setup(x => x.GetCountPerUser(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>());

        // Default: return empty list for Get queries with or without cancellation token

        var mockLogger = new Mock<ILogger<BondAccountImportService>>();
        var importAccountValidator = new ImportAccountValidator(_userPlanVerifier);
        _service = new BondAccountImportService(
            _mockBondEntryRepository.Object,
            importAccountValidator,
            mockLogger.Object);
    }

    private const int _owner = 7;

    // The default mock answers Add with false; owner flows write through the cancellable overload.
    private void SetupAddSucceeds() =>
        _mockBondEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

    private void SetupOwnerRows(int bondDetailsId, params BondAccountEntry[] rows) =>
        _mockBondEntryRepository.Setup(x => x.GetByOwner(_owner, bondDetailsId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(rows.ToAsyncEnumerable());

    private static BondAccountEntry OwnerRow(long id, DateTime date, decimal valueChange, int bondDetailsId, int? accountId = null) =>
        new(accountId, id, date, 0, valueChange, bondDetailsId) { UserId = _owner };

    [Fact]
    public async Task ImportOwnerEntries_ProbesEachNamedDefinition_StoresUnlinkedRows_AndNeverRecalculates()
    {
        var date = new DateTime(2025, 3, 4, 10, 0, 0, DateTimeKind.Utc);
        SetupAddSucceeds();
        SetupOwnerRows(2);
        SetupOwnerRows(3);
        List<BondAccountEntry>? added = null;
        _mockBondEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<BondAccountEntry> entries, CancellationToken _) => (added ??= []).AddRange(entries)).ReturnsAsync(true);

        var result = await _service.ImportOwnerEntries(_owner, [new BondEntryImport(date, 5m, 2, "a"), new BondEntryImport(date, 6m, 3, "b")], TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Imported);
        Assert.All(added!, e => { Assert.Null(e.AccountId); Assert.Equal(_owner, e.UserId); Assert.Equal(0m, e.Value); });
        Assert.Equal([2, 3], added!.Select(e => e.BondDetailsId).Order());
        _mockBondEntryRepository.Verify(x => x.GetByOwner(_owner, 2, date.Date, date.Date.AddDays(1).AddTicks(-1), It.IsAny<CancellationToken>()), Times.Once);
        _mockBondEntryRepository.Verify(x => x.GetByOwner(_owner, 3, date.Date, date.Date.AddDays(1).AddTicks(-1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportOwnerEntries_ReviewsEachDefinitionOnItsOwn_AndOffersOnlyTheSurplus()
    {
        var date = new DateTime(2025, 3, 4, 10, 0, 0, DateTimeKind.Utc);
        SetupAddSucceeds();
        SetupOwnerRows(2, OwnerRow(1, date, 10m, 2, accountId: 9), OwnerRow(2, date, 10m, 2), OwnerRow(3, date, 99m, 2));
        SetupOwnerRows(3, OwnerRow(4, date, 1m, 3));
        BondEntryImport[] incoming =
        [
            new(date, 10m, 2, "first"), new(date, 10m, 2, "second"), new(date, 10m, 2, "third"),
            new(date, 5m, 3, "another definition, same day"),
        ];

        var result = await _service.ImportOwnerEntries(_owner, incoming, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Imported); // bond 3 has no overlap, so a bond 2 review never holds it back
        Assert.Equal(2, result.Conflicts.Count(x => x.IsExactMatch));
        var surplus = Assert.Single(result.Conflicts, x => !x.IsExactMatch);
        Assert.Equal("third", surplus.ImportEntry!.Description);
        Assert.DoesNotContain(result.Conflicts, x => x.ExistingEntry?.EntryId == 3);

        // The existing side of a match carries neither the legacy account link nor a running value.
        var matched = result.Conflicts.Where(x => x.IsExactMatch).Select(x => x.ExistingEntry!).ToList();
        Assert.Contains(matched, x => x.EntryId == 1);
        Assert.All(matched, x => { Assert.Null(x.AccountId); Assert.Equal(0m, x.Value); });
    }

    [Fact]
    public async Task ApplyOwnerConflicts_AddsOnlyTheMissingOccurrencesOfEachKey_KeepingTheSurplusMetadata()
    {
        var date = new DateTime(2025, 3, 4, 10, 0, 0, DateTimeKind.Utc);
        SetupOwnerRows(2, OwnerRow(1, date, 10m, 2));
        List<BondAccountEntry> added = [];
        _mockBondEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<BondAccountEntry> entries, CancellationToken _) => added.AddRange(entries)).ReturnsAsync(true);

        var result = await _service.ApplyOwnerConflicts(_owner,
            [new BondEntryImport(date, 10m, 2, "one"), new BondEntryImport(date, 10m, 2, "two"), new BondEntryImport(date, 10m, 2, "three")], TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Imported);
        Assert.Equal(["two", "three"], added.Select(e => e.Description));
        Assert.All(added, e => { Assert.Null(e.AccountId); Assert.Equal(_owner, e.UserId); });
    }

    [Fact]
    public async Task ApplyOwnerConflicts_WhenEverythingExists_WritesNothing()
    {
        var date = new DateTime(2025, 3, 4, 10, 0, 0, DateTimeKind.Utc);
        SetupOwnerRows(2, OwnerRow(1, date, 10m, 2), OwnerRow(2, date, 10m, 2));

        var result = await _service.ApplyOwnerConflicts(_owner, [new BondEntryImport(date, 10m, 2), new BondEntryImport(date, 10m, 2)], TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Imported);
        _mockBondEntryRepository.Verify(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyOwnerConflicts_ChecksThePlanForWhatIsAddedAndBatchesTheWrites()
    {
        var date = new DateTime(2025, 3, 4, 10, 0, 0, DateTimeKind.Utc);
        SetupOwnerRows(2);
        var calls = new List<int>();
        _mockBondEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<BondAccountEntry> entries, CancellationToken _) => calls.Add(entries.Count())).ReturnsAsync(true);

        var result = await _service.ApplyOwnerConflicts(_owner, Enumerable.Range(0, 1_200).Select(_ => new BondEntryImport(date, 1m, 2)), TestContext.Current.CancellationToken);

        Assert.Equal(1_200, result.Imported);
        Assert.Equal([500, 500, 200], calls);

        _mockUserRepository.Setup(x => x.GetUser(It.IsAny<int>())).ReturnsAsync(new User { Login = "Free", UserId = _owner, PricingLevel = PricingLevel.Free, CreationDate = DateTime.UtcNow });
        _mockBondEntryRepository.Setup(x => x.GetEntriesCountPerUser(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [_owner] = int.MaxValue / 2 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ApplyOwnerConflicts(_owner, [new BondEntryImport(date, 1m, 2)], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ApplyOwnerConflicts_ReportsNonUtcRowsAndFailedBatches()
    {
        var date = new DateTime(2025, 3, 4, 10, 0, 0, DateTimeKind.Utc);
        SetupOwnerRows(2);
        _mockBondEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _service.ApplyOwnerConflicts(_owner,
            [new BondEntryImport(date, 1m, 2), new BondEntryImport(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), 2m, 2)], TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Imported);
        Assert.Equal(2, result.Failed);
        Assert.Equal(2, result.Errors.Count);
    }
}