using FinanceManager.Application.FinancialAccounts.Shared.Imports;

namespace FinanceManager.Tests.Unit.Application.FinancialAccounts.Shared;

[Trait("Category", "Unit")]
public class ImportConflictDetectorTests
{
    private record Row(int Key, string Label);

    [Fact]
    public void GetImportsMissingFromExisting_ReportsTheTrailingSurplus_EachWithItsOwnMetadata()
    {
        Row[] imports = [new(1, "a"), new(1, "b"), new(1, "c"), new(2, "d")];
        Row[] existing = [new(1, "x")];

        var surplus = ImportConflictDetector.GetImportsMissingFromExisting(imports, existing, i => i.Key, e => e.Key).ToList();

        Assert.Equal(["b", "c", "d"], surplus.Select(x => x.Label));
    }

    [Fact]
    public void GetImportsMissingFromExisting_WhenExistingHasAtLeastAsMany_ReportsNothing()
    {
        Row[] imports = [new(1, "a"), new(1, "b")];
        Row[] existing = [new(1, "x"), new(1, "y"), new(1, "z")];

        Assert.Empty(ImportConflictDetector.GetImportsMissingFromExisting(imports, existing, i => i.Key, e => e.Key));
    }

    [Fact]
    public void GetExactMatches_PairsTheSmallerCountOfEachKey()
    {
        Row[] imports = [new(1, "a"), new(1, "b"), new(2, "c")];
        Row[] existing = [new(1, "x")];

        var matches = ImportConflictDetector.GetExactMatches(imports, existing, i => i.Key, e => e.Key).ToList();

        var match = Assert.Single(matches);
        Assert.Equal(("a", "x"), (match.Import.Label, match.Existing.Label));
    }
}