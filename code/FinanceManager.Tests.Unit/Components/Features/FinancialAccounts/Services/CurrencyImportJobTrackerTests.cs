using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Services;

[Trait("Category", "Unit")]
public class CurrencyImportJobTrackerTests
{
    [Fact]
    public void MergeConflicts_PreservesResolvedStateAndLiveOnlyConflicts()
    {
        var resolved = Conflict("a") with { IsResolved = true };
        var liveOnly = Conflict("b");
        var incoming = Conflict("a");

        var merged = CurrencyImportJobTracker.MergeConflicts([resolved, liveOnly], [incoming]);

        Assert.Contains(merged, x => x.ConflictId == "a" && x.IsResolved);
        Assert.Contains(merged, x => x.ConflictId == "b");
    }

    private static ImportJobConflict Conflict(string id) =>
        new(id, new ImportConflict(1, null, null, "test"));
}