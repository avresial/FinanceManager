using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Enums;

namespace FinanceManager.UnitTests.Domain.Entities.Bonds;

public class BondAccountTests
{
    [Fact]
    public void Add_ShouldAddEntry()
    {
        // Arrange
        var account = new BondAccount(1, 1, "Test Account", AccountLabel.Other);
        var entry = new BondAccountEntry(1, 1, DateTime.Now, 100m, 10m, 1);

        // Act
        account.Add(entry);

        // Assert
        Assert.Single(account.Entries);
        Assert.Contains(entry, account.Entries);
    }

    [Fact]
    public void UpdateEntry_ShouldUpdateExistingEntry()
    {
        // Arrange
        var account = new BondAccount(1, 1, "Test Account", AccountLabel.Other);
        var entry = new BondAccountEntry(1, 1, DateTime.Now, 100m, 10m, 1);
        account.Add(entry);

        var updatePayload = new BondAccountEntry(1, 1, DateTime.Now.AddDays(1), 200m, 20m, 2);

        // Act
        account.UpdateEntry(updatePayload);

        // Assert
        Assert.Single(account.Entries);
        var result = account.Entries.First();
        Assert.Equal(updatePayload.BondDetailsId, result.BondDetailsId);
        Assert.Equal(updatePayload.ValueChange, result.ValueChange);
    }

    [Fact]
    public void Remove_ShouldRemoveEntry()
    {
        // Arrange
        var account = new BondAccount(1, 1, "Test Account", AccountLabel.Other);
        var entry = new BondAccountEntry(1, 1, DateTime.Now, 100m, 10m, 1);
        account.Add(entry);

        // Act
        account.Remove(entry.EntryId);

        // Assert
        Assert.Empty(account.Entries);
    }

    [Fact]
    public async Task GetDailyPrice_ShouldReturnCorrectValues()
    {
        // Arrange
        var postingDate = new DateTime(2023, 1, 1);
        var endDate = postingDate.AddDays(4); // Jan 5

        var entry1 = new BondAccountEntry(1, 1, postingDate, 0, 100m, 1);
        var entry2 = new BondAccountEntry(1, 2, endDate, 0, 100m, 1); // Extends End to Jan 5

        var account = new BondAccount(1, 1, "Test Account", AccountLabel.Other);
        account.Add(entry1);
        account.Add(entry2);

        var calculationMethod = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2024-01-01",
            Rate = 0.0365m // 3.65% -> 0.01 daily on 100
        };

        var bondDetails = new BondDetails(
            "Test Bond",
            "Issuer",
            DateOnly.FromDateTime(postingDate),
            DateOnly.FromDateTime(postingDate.AddYears(1)),
            [calculationMethod]
        )
        { Id = 1 };

        // Act
        var result = account.GetDailyPrice(DateOnly.FromDateTime(postingDate), DateOnly.FromDateTime(endDate), [bondDetails]);

        // Assert
        Assert.Equal(5, result.Count);
        Assert.Equal(100.01m, result[DateOnly.FromDateTime(postingDate.AddDays(1))]);
        Assert.Equal(200.04m, result[DateOnly.FromDateTime(endDate)], 2);
    }
}