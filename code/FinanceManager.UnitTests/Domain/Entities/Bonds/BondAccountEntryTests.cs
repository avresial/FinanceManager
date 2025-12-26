using FinanceManager.Domain.Entities.Bonds;

namespace FinanceManager.UnitTests.Domain.Entities.Bonds;

public class BondAccountEntryTests
{
    [Fact]
    public void Update_ShouldUpdatePropertiesCorrectly()
    {
        // Arrange
        var entry = new BondAccountEntry(1, 1, DateTime.Now, 100m, 10m, 1);
        var updateEntry = new BondAccountEntry(1, 1, DateTime.Now.AddDays(1), 200m, 20m, 2);

        // Act
        entry.Update(updateEntry);

        // Assert
        Assert.Equal(updateEntry.PostingDate, entry.PostingDate);
        Assert.Equal(110m, entry.Value);
        Assert.Equal(updateEntry.ValueChange, entry.ValueChange);
        Assert.Equal(updateEntry.BondDetailsId, entry.BondDetailsId);
    }

    [Fact]
    public void GetCopy_ShouldReturnCorrectCopy()
    {
        // Arrange
        var entry = new BondAccountEntry(1, 1, DateTime.Now, 100m, 10m, 1);

        // Act
        var copy = entry.GetCopy();

        // Assert
        Assert.NotSame(entry, copy);
        Assert.Equal(entry.AccountId, copy.AccountId);
        Assert.Equal(entry.EntryId, copy.EntryId);
        Assert.Equal(entry.PostingDate, copy.PostingDate);
        Assert.Equal(entry.Value, copy.Value);
        Assert.Equal(entry.ValueChange, copy.ValueChange);
        Assert.Equal(entry.BondDetailsId, copy.BondDetailsId);
    }

    [Fact]
    public void GetPrice_ShouldReturnCorrectValues()
    {
        // Arrange
        var postingDate = new DateTime(2023, 1, 1);
        var entry = new BondAccountEntry(1, 1, postingDate, 100m, 100m, 1);

        var calculationMethod = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = FinanceManager.Domain.Enums.DateOperator.UntilDate,
            DateValue = "2024-01-01",
            Rate = 0.0365m // 3.65%
        };

        var bondDetails = new BondDetails(
            "Test Bond",
            "Issuer",
            DateOnly.FromDateTime(postingDate),
            DateOnly.FromDateTime(postingDate.AddYears(1)),
            [calculationMethod]
        );

        var targetDate = DateOnly.FromDateTime(postingDate.AddDays(2));

        // Act
        var result = entry.GetPrice(targetDate, bondDetails);

        // Assert

        Assert.Equal(3, result.Count);
        Assert.Equal(100m, result[DateOnly.FromDateTime(postingDate)]);
        Assert.Equal(100.01m, result[DateOnly.FromDateTime(postingDate.AddDays(1))]);
    }
}