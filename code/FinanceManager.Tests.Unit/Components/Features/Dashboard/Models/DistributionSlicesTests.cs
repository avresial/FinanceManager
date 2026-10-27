using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Shared.Charting;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Models;

[Trait("Category", "Unit")]
public class DistributionSlicesTests
{
    [Fact]
    public void Build_SortsByValue_AndComputesPercentages()
    {
        var slices = DistributionSlices.Build([new("Loan", 25m), new("Mortgage", 75m)]);

        Assert.Equal(["Mortgage", "Loan"], slices.Select(x => x.Name));
        Assert.Equal([75.0m, 25.0m], slices.Select(x => x.Percent));
        Assert.All(slices, slice => Assert.False(slice.IsOther));
    }

    [Fact]
    public void Build_SixItems_KeepsEveryNameWithDistinctColours()
    {
        var slices = DistributionSlices.Build(Items(6));

        Assert.Equal(6, slices.Count);
        Assert.DoesNotContain(slices, x => x.IsOther);
        Assert.Equal(6, slices.Select(x => x.Color).Distinct().Count());
        Assert.NotEqual(slices[0].Color, slices[1].Color);
    }

    [Fact]
    public void Build_GroupsItemsAfterTopFive_IntoOther()
    {
        var slices = DistributionSlices.Build(Items(8));

        Assert.Equal(6, slices.Count);
        Assert.Equal(["Item 8", "Item 7", "Item 6", "Item 5", "Item 4"], slices.Take(5).Select(x => x.Name));
        var other = slices[^1];
        Assert.True(other.IsOther);
        Assert.Equal(DistributionSlices.OtherName, other.Name);
        Assert.Equal(1m + 2m + 3m, other.Value);
        Assert.Equal(["Item 3", "Item 2", "Item 1"], other.Members.Select(x => x.Name));
        Assert.Equal(ColorsProvider.OtherColor, other.Color);
        Assert.Null(other.AccountId);
        Assert.Equal(5, slices.Take(5).Select(x => x.Color).Distinct().Count());
    }

    [Fact]
    public void Build_IgnoresNonpositiveItems_AndReturnsEmptyWithoutPositiveTotal()
    {
        Assert.Empty(DistributionSlices.Build([new("Mortgage", 0m), new("Loan", -10m)]));

        var slices = DistributionSlices.Build([new("Stock", 50m), new("Debt", -10m)]);

        Assert.Equal("Stock", Assert.Single(slices).Name);
        Assert.Equal(100.0m, slices[0].Percent);
    }

    [Fact]
    public void Build_KeepsAccountIds()
    {
        var slices = DistributionSlices.Build([new("Main", 60m) { AccountId = 7 }, new("Legacy", 40m)]);

        Assert.Equal(7, slices[0].AccountId);
        Assert.Null(slices[1].AccountId);
    }

    [Fact]
    public void DistributionPalette_HasEightDistinctColours_ThatDifferFromOther()
    {
        var colors = ColorsProvider.GetDistributionColors();

        Assert.True(colors.Count >= 8);
        Assert.Equal(colors.Count, colors.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain(ColorsProvider.OtherColor, colors, StringComparer.OrdinalIgnoreCase);
    }

    private static List<NameValueResult> Items(int count) =>
        [.. Enumerable.Range(1, count).Select(i => new NameValueResult($"Item {i}", i))];
}