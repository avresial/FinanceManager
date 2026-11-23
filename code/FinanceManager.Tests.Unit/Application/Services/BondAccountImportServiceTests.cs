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
    private readonly Mock<IAccountRepository<BondAccount>> _mockAccountRepository;
    private readonly Mock<IBondAccountEntryRepository<BondAccountEntry>> _mockBondEntryRepository;
    private readonly Mock<IAccountEntryRepository<CurrencyAccountEntry>> _mockCurrencyEntryRepository;
    private readonly Mock<IInvestmentTransactionRepository> _mockInvestmentTransactionRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly UserPlanVerifier _userPlanVerifier;
    private readonly BondAccountImportService _service;

    public BondAccountImportServiceTests()
    {
        _mockAccountRepository = new Mock<IAccountRepository<BondAccount>>();
        _mockBondEntryRepository = new Mock<IBondAccountEntryRepository<BondAccountEntry>>();
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
        _mockBondEntryRepository.Setup(x => x.Get(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(new List<BondAccountEntry>().ToAsyncEnumerable());
        _mockBondEntryRepository.Setup(x => x.Get(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new List<BondAccountEntry>().ToAsyncEnumerable());

        var mockLogger = new Mock<ILogger<BondAccountImportService>>();
        var importAccountValidator = new ImportAccountValidator(_userPlanVerifier);
        _service = new BondAccountImportService(
            _mockAccountRepository.Object,
            _mockBondEntryRepository.Object,
            importAccountValidator,
            mockLogger.Object);
    }

    [Fact]
    public async Task ImportEntries_EmptyList_ReturnsZeroImported()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _service.ImportEntries(1, 1, [], ct);

        Assert.Equal(0, result.Imported);
        Assert.Equal(0, result.Failed);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Conflicts);
        _mockBondEntryRepository.Verify(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportEntries_NullEntries_ThrowsArgumentNullException()
    {
        var ct = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.ImportEntries(1, 1, null!, ct));
    }

    [Fact]
    public async Task ImportEntries_WhenCallerCancels_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

#pragma warning disable xUnit1051
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.ImportEntries(1, 1, [], cancellationToken: cancellation.Token));
#pragma warning restore xUnit1051
    }

    [Fact]
    public async Task ImportEntries_WhenCallerCancelsAfterCommit_RecalculatesCommittedEntriesBeforePropagating()
    {
        const int userId = 1;
        const int accountId = 10;
        var account = new BondAccount(userId, accountId, "TestBonds");
        _mockAccountRepository.Setup(x => x.Get(accountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);

        using var cancellation = new CancellationTokenSource();
        var date = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        List<BondEntryImport> entries = [new(date, 100m, 1)];

        _mockBondEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), false, It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<BondAccountEntry>, bool, CancellationToken>((batch, _, _) =>
            {
                foreach (var entry in batch)
                    typeof(BondAccountEntry).GetProperty(nameof(BondAccountEntry.EntryId))?.SetValue(entry, 100);
                cancellation.Cancel();
            })
            .ReturnsAsync(true);
        _mockBondEntryRepository.Setup(x => x.RecalculateValues(accountId, 100, It.Is<CancellationToken>(token => token == cancellation.Token)))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));
        _mockBondEntryRepository.Setup(x => x.RecalculateValues(accountId, 100, CancellationToken.None))
            .Returns(Task.CompletedTask);

#pragma warning disable xUnit1051
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.ImportEntries(userId, accountId, entries, cancellation.Token));
#pragma warning restore xUnit1051

        _mockBondEntryRepository.Verify(
            x => x.RecalculateValues(accountId, 100, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task ImportEntries_AccountNotFound_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        _mockAccountRepository.Setup(x => x.Get(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BondAccount?)null);
        _mockAccountRepository.Setup(x => x.Get(1))
            .ReturnsAsync((BondAccount?)null);

        var entries = new List<BondEntryImport>
        {
            new(DateTime.UtcNow.Date, 100m, 1)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ImportEntries(1, 1, entries, ct));
    }

    [Fact]
    public async Task ImportEntries_UserNotOwner_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var account = new BondAccount(2, 1, "OtherUserAccount");
        _mockAccountRepository.Setup(x => x.Get(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _mockAccountRepository.Setup(x => x.Get(1))
            .ReturnsAsync(account);

        var entries = new List<BondEntryImport>
        {
            new(DateTime.UtcNow.Date, 100m, 1)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ImportEntries(1, 1, entries, ct));
    }

    [Fact]
    public async Task ImportEntries_SuccessfulBatch_PersistsInSingleBatchAndRecalculatesOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        const int userId = 1;
        const int accountId = 10;
        var account = new BondAccount(userId, accountId, "TestBonds");
        _mockAccountRepository.Setup(x => x.Get(accountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);

        var day1 = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var day2 = new DateTime(2025, 1, 10, 1, 0, 0, DateTimeKind.Utc);
        List<BondEntryImport> domainEntries =
        [
            new(day1, 100m, 1),
            new(day2, 200m, 2)
        ];

        _mockBondEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), false, It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<BondAccountEntry>, bool, CancellationToken>((entries, _, _) =>
            {
                int id = 100;
                foreach (var e in entries)
                    typeof(BondAccountEntry).GetProperty(nameof(BondAccountEntry.EntryId))?.SetValue(e, id++);
            })
            .ReturnsAsync(true);
        _mockBondEntryRepository.Setup(x => x.RecalculateValues(accountId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.ImportEntries(userId, accountId, domainEntries, ct);

        Assert.Equal(accountId, result.AccountId);
        Assert.Equal(2, result.Imported);
        Assert.Equal(0, result.Failed);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Conflicts);

        // Exactly one batch Add call
        _mockBondEntryRepository.Verify(
            x => x.Add(It.Is<IEnumerable<BondAccountEntry>>(e => e.Count() == 2), false, It.IsAny<CancellationToken>()),
            Times.Once);

        // Exactly one recalculation call using the generated ID anchor (100)
        _mockBondEntryRepository.Verify(
            x => x.RecalculateValues(accountId, 100, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ImportEntries_KeepsImportedDescription()
    {
        var ct = TestContext.Current.CancellationToken;
        const int userId = 1;
        const int accountId = 10;
        var account = new BondAccount(userId, accountId, "TestBonds");
        _mockAccountRepository.Setup(x => x.Get(accountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);
        List<BondAccountEntry> saved = [];
        _mockBondEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), false, It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<BondAccountEntry>, bool, CancellationToken>((entries, _, _) => saved.AddRange(entries))
            .ReturnsAsync(true);
        _mockBondEntryRepository.Setup(x => x.RecalculateValues(accountId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        BondEntryImport[] imports = [new(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), 5m, 1, "Top up")];

        var result = await _service.ImportEntries(userId, accountId, imports, ct);

        Assert.Equal(1, result.Imported);
        Assert.Equal("Top up", Assert.Single(saved).Description);
    }

    [Fact]
    public async Task ImportEntries_UsesBoundedPersistenceBatches()
    {
        var ct = TestContext.Current.CancellationToken;
        const int userId = 1;
        const int accountId = 10;
        var account = new BondAccount(userId, accountId, "TestBonds");
        _mockAccountRepository.Setup(x => x.Get(accountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);

        var date = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var domainEntries = Enumerable.Range(0, 1_001)
            .Select(i => new BondEntryImport(date.AddMinutes(i), i + 1m, 1))
            .ToArray();
        var batchSizes = new List<int>();
        _mockBondEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), false, It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<BondAccountEntry>, bool, CancellationToken>((batch, _, _) => batchSizes.Add(batch.Count()))
            .ReturnsAsync(true);

        var result = await _service.ImportEntries(userId, accountId, domainEntries, ct);

        Assert.Equal(1_001, result.Imported);
        Assert.Equal(0, result.Failed);
        Assert.Equal([500, 500, 1], batchSizes);
    }

    [Fact]
    public async Task ImportEntries_WhenRepositoryAddFails_MarksAllEntriesFailed()
    {
        var ct = TestContext.Current.CancellationToken;
        const int userId = 1;
        const int accountId = 10;
        var account = new BondAccount(userId, accountId, "TestBonds");
        _mockAccountRepository.Setup(x => x.Get(accountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);

        var day1 = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        List<BondEntryImport> domainEntries =
        [
            new(day1, 100m, 1),
            new(day1.AddHours(1), 200m, 1)
        ];

        _mockBondEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.ImportEntries(userId, accountId, domainEntries, ct);

        Assert.Equal(0, result.Imported);
        Assert.Equal(2, result.Failed);
        Assert.Equal(2, result.Errors.Count);
        _mockBondEntryRepository.Verify(x => x.RecalculateValues(It.IsAny<int>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportEntries_InvalidPostingDateKind_FailsIndividuallyAndBatchesValidEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        const int userId = 1;
        const int accountId = 10;
        var account = new BondAccount(userId, accountId, "TestBonds");
        _mockAccountRepository.Setup(x => x.Get(accountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);

        var validDate = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var invalidDate = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Unspecified);
        List<BondEntryImport> domainEntries =
        [
            new(validDate, 100m, 1),
            new(invalidDate, 200m, 1)
        ];

        _mockBondEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), false, It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<BondAccountEntry>, bool, CancellationToken>((entries, _, _) =>
            {
                foreach (var e in entries)
                    typeof(BondAccountEntry).GetProperty(nameof(BondAccountEntry.EntryId))?.SetValue(e, 50);
            })
            .ReturnsAsync(true);
        _mockBondEntryRepository.Setup(x => x.RecalculateValues(accountId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.ImportEntries(userId, accountId, domainEntries, ct);

        Assert.Equal(1, result.Imported);
        Assert.Equal(1, result.Failed);
        Assert.Single(result.Errors);
        Assert.Contains("is not UTC", result.Errors[0]);

        _mockBondEntryRepository.Verify(
            x => x.Add(It.Is<IEnumerable<BondAccountEntry>>(e => e.Count() == 1), false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ImportEntries_ReverseDayConflictHandling_SkipsConflictDayAndBatchesCleanDay()
    {
        var ct = TestContext.Current.CancellationToken;
        const int userId = 1;
        const int accountId = 10;
        var account = new BondAccount(userId, accountId, "TestBonds");
        _mockAccountRepository.Setup(x => x.Get(accountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);

        var jan10 = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var jan15 = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        // Existing entry matches jan15 exactly
        var existingEntry = new BondAccountEntry(accountId, 101, jan15, 500m, 500m, 1);
        _mockBondEntryRepository.Setup(x => x.Get(accountId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(new List<BondAccountEntry> { existingEntry }.ToAsyncEnumerable());
        _mockBondEntryRepository.Setup(x => x.Get(accountId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new List<BondAccountEntry> { existingEntry }.ToAsyncEnumerable());

        List<BondEntryImport> domainEntries =
        [
            new(jan10, 100m, 1), // Clean day
            new(jan15, 500m, 1)  // Conflict day (exact match)
        ];

        _mockBondEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), false, It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<BondAccountEntry>, bool, CancellationToken>((entries, _, _) =>
            {
                foreach (var e in entries)
                    typeof(BondAccountEntry).GetProperty(nameof(BondAccountEntry.EntryId))?.SetValue(e, 42);
            })
            .ReturnsAsync(true);
        _mockBondEntryRepository.Setup(x => x.RecalculateValues(accountId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.ImportEntries(userId, accountId, domainEntries, ct);

        Assert.Equal(1, result.Imported);
        Assert.Equal(0, result.Failed);
        Assert.Single(result.Conflicts);
        Assert.Equal("Exact match", result.Conflicts[0].Reason);

        // Jan 10 was batched and persisted
        _mockBondEntryRepository.Verify(
            x => x.Add(It.Is<IEnumerable<BondAccountEntry>>(e => e.Count() == 1 && e.First().PostingDate == jan10), false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ImportEntries_RecalculationTimeout_MarksFailedWithoutThrowing()
    {
        const int userId = 1;
        const int accountId = 10;
        var account = new BondAccount(userId, accountId, "TestBonds");
        _mockAccountRepository.Setup(x => x.Get(accountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);

        var jan10 = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        List<BondEntryImport> domainEntries = [new(jan10, 100m, 1)];

        _mockBondEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), false, It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<BondAccountEntry>, bool, CancellationToken>((entries, _, _) =>
            {
                foreach (var e in entries)
                    typeof(BondAccountEntry).GetProperty(nameof(BondAccountEntry.EntryId))?.SetValue(e, 77);
            })
            .ReturnsAsync(true);
        _mockBondEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), false))
            .Callback<IEnumerable<BondAccountEntry>, bool>((entries, _) =>
            {
                foreach (var e in entries)
                    typeof(BondAccountEntry).GetProperty(nameof(BondAccountEntry.EntryId))?.SetValue(e, 77);
            })
            .ReturnsAsync(true);

        // Internal OperationCanceledException (not from caller token)
        _mockBondEntryRepository.Setup(x => x.RecalculateValues(accountId, It.IsAny<long>()))
            .ThrowsAsync(new OperationCanceledException());
        _mockBondEntryRepository.Setup(x => x.RecalculateValues(accountId, It.IsAny<long>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

#pragma warning disable xUnit1051
        var result = await _service.ImportEntries(userId, accountId, domainEntries, CancellationToken.None);
#pragma warning restore xUnit1051

        Assert.Equal(1, result.Imported);
        Assert.Equal(1, result.Failed);
        Assert.Single(result.Errors);
        Assert.Contains("Recalculation cancelled or timed out", result.Errors[0]);
        _mockBondEntryRepository.Verify(
            x => x.RecalculateValues(accountId, 77, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task ApplyResolvedConflicts_DeletesExistingAndAddsImported()
    {
        var ct = TestContext.Current.CancellationToken;
        var conflict = new ResolvedBondImportConflict
        {
            AccountId = 10,
            ExistingId = 55,
            LeaveExisting = false,
            AddImported = true,
            ImportData = new BondEntryImport(new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc), 150m, 2)
        };

        _mockBondEntryRepository.Setup(x => x.Delete(10, 55, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockBondEntryRepository.Setup(x => x.Add(It.IsAny<BondAccountEntry>(), true, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await _service.ApplyResolvedConflicts([conflict], ct);

        _mockBondEntryRepository.Verify(x => x.Delete(10, 55, It.IsAny<CancellationToken>()), Times.Once);
        _mockBondEntryRepository.Verify(x => x.Add(It.Is<BondAccountEntry>(e => e.AccountId == 10 && e.BondDetailsId == 2), true, It.IsAny<CancellationToken>()), Times.Once);
    }

    private const int _owner = 7;

    // The default mock answers Add with false; owner flows write through the cancellable overload.
    private void SetupAddSucceeds() =>
        _mockBondEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

    private void SetupOwnerRows(int bondDetailsId, params BondAccountEntry[] rows) =>
        _mockBondEntryRepository.Setup(x => x.GetByOwner(_owner, bondDetailsId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(rows.ToAsyncEnumerable());

    private static BondAccountEntry OwnerRow(long id, DateTime date, decimal valueChange, int bondDetailsId, int? accountId = null) =>
        new(accountId, id, date, 0, valueChange, bondDetailsId) { UserId = _owner };

    [Fact]
    public async Task ImportEntries_UnrelatedExistingRowOnTheSameDay_ImportsWithoutConflict()
    {
        var accountId = 1;
        var date = new DateTime(2025, 3, 4, 10, 0, 0, DateTimeKind.Utc);
        SetupAddSucceeds();
        _mockAccountRepository.Setup(x => x.Get(accountId, It.IsAny<CancellationToken>())).ReturnsAsync(new BondAccount(1, accountId, "Test", AccountLabel.Other));
        _mockBondEntryRepository.Setup(x => x.Get(accountId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(new[] { new BondAccountEntry(accountId, 1, date, 10m, 10m, 2) }.ToAsyncEnumerable());

        var result = await _service.ImportEntries(1, accountId, [new BondEntryImport(date, 5m, 2)], TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Imported);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public async Task ImportOwnerEntries_ProbesEachNamedDefinition_StoresUnlinkedRows_AndNeverRecalculates()
    {
        var date = new DateTime(2025, 3, 4, 10, 0, 0, DateTimeKind.Utc);
        SetupAddSucceeds();
        SetupOwnerRows(2);
        SetupOwnerRows(3);
        List<BondAccountEntry>? added = null;
        _mockBondEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), false, It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<BondAccountEntry> entries, bool _, CancellationToken _) => (added ??= []).AddRange(entries)).ReturnsAsync(true);

        var result = await _service.ImportOwnerEntries(_owner, [new BondEntryImport(date, 5m, 2, "a"), new BondEntryImport(date, 6m, 3, "b")], TestContext.Current.CancellationToken);

        Assert.Null(result.AccountId);
        Assert.Equal(2, result.Imported);
        Assert.All(added!, e => { Assert.Null(e.AccountId); Assert.Equal(_owner, e.UserId); Assert.Equal(0m, e.Value); });
        Assert.Equal([2, 3], added!.Select(e => e.BondDetailsId).Order());
        _mockBondEntryRepository.Verify(x => x.GetByOwner(_owner, 2, date.Date, date.Date.AddDays(1).AddTicks(-1), It.IsAny<CancellationToken>()), Times.Once);
        _mockBondEntryRepository.Verify(x => x.GetByOwner(_owner, 3, date.Date, date.Date.AddDays(1).AddTicks(-1), It.IsAny<CancellationToken>()), Times.Once);
        _mockBondEntryRepository.Verify(x => x.Get(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockAccountRepository.Verify(x => x.Get(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockAccountRepository.Verify(x => x.Get(It.IsAny<int>()), Times.Never);
        _mockBondEntryRepository.Verify(x => x.RecalculateValues(It.IsAny<int>(), It.IsAny<long>()), Times.Never);
        _mockBondEntryRepository.Verify(x => x.RecalculateValues(It.IsAny<int>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
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
        Assert.Null(surplus.AccountId);
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
        _mockBondEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), false, It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<BondAccountEntry> entries, bool _, CancellationToken _) => added.AddRange(entries)).ReturnsAsync(true);

        var result = await _service.ApplyOwnerConflicts(_owner,
            [new BondEntryImport(date, 10m, 2, "one"), new BondEntryImport(date, 10m, 2, "two"), new BondEntryImport(date, 10m, 2, "three")], TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Imported);
        Assert.Equal(["two", "three"], added.Select(e => e.Description));
        Assert.All(added, e => { Assert.Null(e.AccountId); Assert.Equal(_owner, e.UserId); });
        _mockBondEntryRepository.Verify(x => x.RecalculateValues(It.IsAny<int>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyOwnerConflicts_WhenEverythingExists_WritesNothing()
    {
        var date = new DateTime(2025, 3, 4, 10, 0, 0, DateTimeKind.Utc);
        SetupOwnerRows(2, OwnerRow(1, date, 10m, 2), OwnerRow(2, date, 10m, 2));

        var result = await _service.ApplyOwnerConflicts(_owner, [new BondEntryImport(date, 10m, 2), new BondEntryImport(date, 10m, 2)], TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Imported);
        _mockBondEntryRepository.Verify(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyOwnerConflicts_ChecksThePlanForWhatIsAddedAndBatchesTheWrites()
    {
        var date = new DateTime(2025, 3, 4, 10, 0, 0, DateTimeKind.Utc);
        SetupOwnerRows(2);
        var calls = new List<int>();
        _mockBondEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), false, It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<BondAccountEntry> entries, bool _, CancellationToken _) => calls.Add(entries.Count())).ReturnsAsync(true);

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
        _mockBondEntryRepository.Setup(x => x.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), false, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _service.ApplyOwnerConflicts(_owner,
            [new BondEntryImport(date, 1m, 2), new BondEntryImport(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), 2m, 2)], TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Imported);
        Assert.Equal(2, result.Failed);
        Assert.Equal(2, result.Errors.Count);
    }
}