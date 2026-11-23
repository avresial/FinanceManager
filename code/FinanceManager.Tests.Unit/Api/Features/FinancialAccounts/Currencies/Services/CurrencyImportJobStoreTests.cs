using FinanceManager.Api.Features.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Imports;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;

namespace FinanceManager.Tests.Unit.Api.Features.FinancialAccounts.Currencies.Services;

[Collection("Api")]
[Trait("Category", "Unit")]
public class CurrencyImportJobStoreTests
{
    [Fact]
    public void TryAddConflict_ExactMatchIsNotActionable()
    {
        // Arrange
        var store = new CurrencyImportJobStore();
        var jobId = store.CreateJob(userId: 1, accountId: 10, totalEntries: 1);
        var date = DateTime.UtcNow.Date;
        var conflict = new ImportConflict(
            10,
            new CurrencyEntryImport(date, 100m),
            new CurrencyAccountEntry(10, 5, date, 100m, 100m),
            "Exact match");

        // Act
        var added = store.TryAddConflict(jobId, conflict);
        var found = store.TryGetStatus(jobId, userId: 1, out var status);

        // Assert
        Assert.NotNull(added);
        Assert.True(added.IsResolved);
        Assert.True(found);
        Assert.NotNull(status);
        Assert.Equal(0, status.ConflictCount);
        Assert.Equal(0, status.ResolvedConflictCount);
    }

    [Fact]
    public void TryResolveConflict_UpdatesActionableConflictCounts()
    {
        // Arrange
        var store = new CurrencyImportJobStore();
        var jobId = store.CreateJob(userId: 1, accountId: 10, totalEntries: 1);
        var date = DateTime.UtcNow.Date;
        var conflict = new ImportConflict(
            10,
            new CurrencyEntryImport(date, 100m),
            null,
            "Import not found in existing");

        var added = store.TryAddConflict(jobId, conflict);

        // Act
        var resolved = store.TryResolveConflict(
            jobId,
            userId: 1,
            new CurrencyImportConflictResolutionDecisionDto(added!.ConflictId, PickImported: true, PickExisting: false),
            out var resolvedConflict);
        var found = store.TryGetStatus(jobId, userId: 1, out var status);

        // Assert
        Assert.True(resolved);
        Assert.NotNull(resolvedConflict);
        Assert.True(found);
        Assert.NotNull(status);
        Assert.Equal(1, status.ConflictCount);
        Assert.Equal(1, status.ResolvedConflictCount);
    }

    [Fact]
    public void TryResolveConflict_PickExistingForImportOnlyConflictSkipsImportedEntry()
    {
        // Arrange
        var store = new CurrencyImportJobStore();
        var jobId = store.CreateJob(userId: 1, accountId: 10, totalEntries: 1);
        var date = DateTime.UtcNow.Date;
        var added = store.TryAddConflict(jobId, new ImportConflict(
            10,
            new CurrencyEntryImport(date, 100m),
            null,
            "Import not found in existing"));

        // Act
        var resolved = store.TryResolveConflict(
            jobId,
            userId: 1,
            new CurrencyImportConflictResolutionDecisionDto(added!.ConflictId, PickImported: false, PickExisting: true),
            out var resolvedConflict);

        // Assert
        Assert.True(resolved);
        Assert.NotNull(resolvedConflict);
        Assert.False(resolvedConflict.AddImported);
        Assert.True(resolvedConflict.LeaveExisting);
        Assert.Null(resolvedConflict.ExistingId);
    }

    [Fact]
    public void TryResolveConflict_PickImportedForExistingOnlyConflictDeletesExistingEntry()
    {
        // Arrange
        var store = new CurrencyImportJobStore();
        var jobId = store.CreateJob(userId: 1, accountId: 10, totalEntries: 1);
        var date = DateTime.UtcNow.Date;
        var added = store.TryAddConflict(jobId, new ImportConflict(
            10,
            null,
            new CurrencyAccountEntry(10, 5, date, 100m, 100m),
            "Existing not found in import"));

        // Act
        var resolved = store.TryResolveConflict(
            jobId,
            userId: 1,
            new CurrencyImportConflictResolutionDecisionDto(added!.ConflictId, PickImported: true, PickExisting: false),
            out var resolvedConflict);

        // Assert
        Assert.True(resolved);
        Assert.NotNull(resolvedConflict);
        Assert.False(resolvedConflict.AddImported);
        Assert.False(resolvedConflict.LeaveExisting);
        Assert.Equal(5, resolvedConflict.ExistingId);
    }

    [Fact]
    public void TryMarkCompleted_ReportsCompletedWhenAllActionableConflictsAreResolved()
    {
        // Arrange
        var store = new CurrencyImportJobStore();
        var jobId = store.CreateJob(userId: 1, accountId: 10, totalEntries: 1);
        var date = DateTime.UtcNow.Date;
        var conflict = new ImportConflict(
            10,
            new CurrencyEntryImport(date, 100m),
            null,
            "Import not found in existing");

        var added = store.TryAddConflict(jobId, conflict);
        store.TryResolveConflict(
            jobId,
            userId: 1,
            new CurrencyImportConflictResolutionDecisionDto(added!.ConflictId, PickImported: true, PickExisting: false),
            out _);

        // Act
        store.TryMarkCompleted(jobId, new ImportResult(10, Imported: 0, Failed: 0, Errors: [], Conflicts: [conflict]));
        var found = store.TryGetStatus(jobId, userId: 1, out var status);

        // Assert
        Assert.True(found);
        Assert.NotNull(status);
        Assert.Equal(AsyncImportJobState.Completed, status.Status);
        Assert.True(status.IsCompleted);
        Assert.Equal(0, status.ConflictCount - status.ResolvedConflictCount);
    }

    [Fact]
    public void CreateOwnerJob_StoresTheCurrencyScopeWithoutAnAccount_AndOnlyTheOwnerCanReadIt()
    {
        var store = new CurrencyImportJobStore();
        var legacy = store.CreateJob(userId: 1, accountId: 10, totalEntries: 1);
        var owner = store.CreateOwnerJob(userId: 1, currencyId: 3, totalEntries: 1);
        var sameOwnerOtherCurrency = store.CreateOwnerJob(userId: 1, currencyId: 4, totalEntries: 1);

        Assert.True(store.TryGetScope(legacy, 1, out var legacyScope));
        Assert.Equal(new CurrencyImportJobScope(10, null), legacyScope);
        Assert.True(store.TryGetScope(owner, 1, out var ownerScope));
        Assert.Equal(new CurrencyImportJobScope(null, 3), ownerScope);
        Assert.True(store.TryGetScope(sameOwnerOtherCurrency, 1, out var otherScope));
        Assert.Equal(new CurrencyImportJobScope(null, 4), otherScope);
        Assert.NotEqual(owner, sameOwnerOtherCurrency);
        Assert.False(store.TryGetScope(owner, 2, out _));
        Assert.False(store.TryGetStatus(owner, 2, out _));
        Assert.False(store.CanAccessJob(owner, 2));
        Assert.False(store.TryGetScope(Guid.NewGuid(), 1, out _));
    }

    [Fact]
    public void TryResolveConflict_OwnerConflictWithoutAnAccount_ResolvesAgainstTheJobScope_AndRejectsAnotherJobsConflictId()
    {
        var store = new CurrencyImportJobStore();
        var job = store.CreateOwnerJob(userId: 1, currencyId: 3, totalEntries: 1);
        var otherJob = store.CreateOwnerJob(userId: 1, currencyId: 4, totalEntries: 1);
        var conflict = new ImportConflict(null, new CurrencyEntryImport(DateTime.UtcNow.Date, 100m, "Shop", "Surplus"), null, "Import not found in existing");
        var added = store.TryAddConflict(job, conflict);

        Assert.False(store.TryResolveConflict(otherJob, 1, new(added!.ConflictId, PickImported: true, PickExisting: false), out _));
        Assert.True(store.TryResolveConflict(job, 1, new(added.ConflictId, PickImported: true, PickExisting: false), out var resolved));
        Assert.True(resolved!.AddImported);
        Assert.Equal("Surplus", resolved.ImportData!.Description);
        Assert.Null(resolved.ExistingId);

        // A repeated decision is a no-op.
        Assert.True(store.TryResolveConflict(job, 1, new(added.ConflictId, PickImported: true, PickExisting: false), out var again));
        Assert.Null(again);
    }

    [Fact]
    public void TryReopenConflict_MakesAFailedDecisionRetryable()
    {
        var store = new CurrencyImportJobStore();
        var job = store.CreateOwnerJob(userId: 1, currencyId: 3, totalEntries: 1);
        var added = store.TryAddConflict(job, new ImportConflict(null, new CurrencyEntryImport(DateTime.UtcNow.Date, 1m), null, "Import not found in existing"));
        var decision = new CurrencyImportConflictResolutionDecisionDto(added!.ConflictId, PickImported: true, PickExisting: false);
        store.TryResolveConflict(job, 1, decision, out _);

        Assert.False(store.TryReopenConflict(job, 2, added.ConflictId)); // not the owner
        Assert.False(store.TryReopenConflict(job, 1, "unknown"));
        Assert.True(store.TryReopenConflict(job, 1, added.ConflictId));

        store.TryGetStatus(job, 1, out var status);
        Assert.Equal(0, status!.ResolvedConflictCount);
        Assert.True(store.TryResolveConflict(job, 1, decision, out var retried));
        Assert.NotNull(retried);
    }
}