using FinanceManager.Api.Services;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Imports;

namespace FinanceManager.UnitTests.Api.Services;

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
}