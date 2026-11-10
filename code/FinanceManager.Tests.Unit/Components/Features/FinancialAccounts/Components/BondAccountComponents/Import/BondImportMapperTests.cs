using FinanceManager.Components.Features.FinancialAccounts.Components.BondAccountComponents.Import;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Imports;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Components.BondAccountComponents.Import;

public class BondImportMapperTests
{
    private static readonly List<string> _headers = ["PostingDate", "ValueChange", "Bond", "Description"];
    private static readonly List<List<string>> _rows = [["2026-03-01 10:00:00", "5", "1", " Top up "]];
    private static readonly BondDetails[] _bonds = [new() { Id = 1, Name = "EDO", Type = default, Currency = DefaultCurrency.PLN }];

    [Fact]
    public void MapEntries_WithDescriptionHeader_ReturnsTrimmedDescription()
    {
        var entry = Assert.Single(BondImportMapper.MapEntries("PostingDate", "ValueChange", "Bond", _headers, _rows, _bonds, "Description"));

        Assert.Equal("Top up", entry.Description);
    }

    [Fact]
    public void MapEntries_WithoutDescriptionHeader_ReturnsEmptyDescription()
    {
        var entry = Assert.Single(BondImportMapper.MapEntries("PostingDate", "ValueChange", "Bond", _headers, _rows, _bonds));

        Assert.Equal(string.Empty, entry.Description);
    }

    [Fact]
    public void ResolvedConflict_ToEntry_KeepsImportedDescription()
    {
        var import = new BondEntryImport(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), 5m, 1, "Top up");

        var entry = new ResolvedBondImportConflict(10, true, import, false, null).ToEntry();

        Assert.Equal("Top up", entry.Description);
    }
}