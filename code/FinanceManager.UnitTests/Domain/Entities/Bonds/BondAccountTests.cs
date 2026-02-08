using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Enums;

namespace FinanceManager.UnitTests.Domain.Entities.Bonds;

[Collection("Domain")]
[Trait("Category", "Unit")]
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

    [Fact]
    public async Task GetDailyPrice_MultipleBondsWithDifferentRates_ShouldAggregateCorrectly()
    {
        // Arrange
        var postingDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        // Bond 1: 1000 at 3.65%
        var entry1 = new BondAccountEntry(1, 1, postingDate, 0, 1000m, 1);
        
        // Bond 2: 2000 at 5% (different bond)
        var entry2 = new BondAccountEntry(1, 2, postingDate, 0, 2000m, 2);

        var account = new BondAccount(1, 1, "Test Account", AccountLabel.Other);
        account.Add(entry1);
        account.Add(entry2);

        var calculationMethod1 = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2024-01-01",
            Rate = 0.0365m
        };

        var calculationMethod2 = new BondCalculationMethod
        {
            Id = 2,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2024-01-01",
            Rate = 0.05m
        };

        var bondDetails1 = new BondDetails(
            "Bond 3.65%",
            "Issuer A",
            DateOnly.FromDateTime(postingDate),
            DateOnly.FromDateTime(postingDate.AddYears(1)),
            [calculationMethod1]
        ) { Id = 1 };

        var bondDetails2 = new BondDetails(
            "Bond 5%",
            "Issuer B",
            DateOnly.FromDateTime(postingDate),
            DateOnly.FromDateTime(postingDate.AddYears(1)),
            [calculationMethod2]
        ) { Id = 2 };

        var targetDate = DateOnly.FromDateTime(postingDate.AddDays(30));

        // Act
        var result = account.GetDailyPrice(
            DateOnly.FromDateTime(postingDate), 
            targetDate, 
            [bondDetails1, bondDetails2]);

        // Assert - Should aggregate both bonds
        Assert.Equal(31, result.Count); // 31 days (0 through 30)
        
        // Day 0 should be 3000 (1000 + 2000)
        Assert.Equal(3000m, result[DateOnly.FromDateTime(postingDate)]);
        
        // Day 30 should be more than 3000 (both growing at different rates)
        var day30Value = result[targetDate];
        Assert.True(day30Value > 3000m, $"Expected value > 3000, got {day30Value}");
        
        // Bond with higher rate should contribute more to growth
        // Approximate: 1000 * (1 + 0.0365/365)^30 + 2000 * (1 + 0.05/365)^30
        Assert.True(day30Value > 3010m && day30Value < 3020m,
            $"Expected aggregated value around 3011-3012, got {day30Value}");
    }

    [Fact]
    public async Task GetDailyPrice_SameDayBuyAndSell_ShouldHandleCorrectly()
    {
        // Arrange - Buy and sell on same day should net to zero
        var postingDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        var buyEntry = new BondAccountEntry(1, 1, postingDate, 0, 1000m, 1);
        var sellEntry = new BondAccountEntry(1, 2, postingDate, 0, -1000m, 1);

        var account = new BondAccount(1, 1, "Test Account", AccountLabel.Other);
        account.Add(buyEntry);
        account.Add(sellEntry);

        var calculationMethod = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2024-01-01",
            Rate = 0.0365m
        };

        var bondDetails = new BondDetails(
            "Test Bond",
            "Issuer",
            DateOnly.FromDateTime(postingDate),
            DateOnly.FromDateTime(postingDate.AddYears(1)),
            [calculationMethod]
        ) { Id = 1 };

        var targetDate = DateOnly.FromDateTime(postingDate.AddDays(10));

        // Act
        var result = account.GetDailyPrice(
            DateOnly.FromDateTime(postingDate), 
            targetDate, 
            [bondDetails]);

        // Assert - Should be zero or near-zero throughout
        Assert.Equal(0m, result[DateOnly.FromDateTime(postingDate)]);
        Assert.Equal(0m, result[targetDate], 2);
    }

    [Fact]
    public async Task GetDailyPrice_EntriesSpreadOverTime_ShouldAccumulateCorrectly()
    {
        // Arrange - Add more capital at different dates
        var startDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        var entry1 = new BondAccountEntry(1, 1, startDate, 0, 1000m, 1);
        var entry2 = new BondAccountEntry(1, 2, startDate.AddDays(30), 0, 500m, 1);
        var entry3 = new BondAccountEntry(1, 3, startDate.AddDays(60), 0, 250m, 1);

        var account = new BondAccount(1, 1, "Test Account", AccountLabel.Other);
        account.Add(entry1);
        account.Add(entry2);
        account.Add(entry3);

        var calculationMethod = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2024-01-01",
            Rate = 0.0365m
        };

        var bondDetails = new BondDetails(
            "Test Bond",
            "Issuer",
            DateOnly.FromDateTime(startDate),
            DateOnly.FromDateTime(startDate.AddYears(1)),
            [calculationMethod]
        ) { Id = 1 };

        var targetDate = DateOnly.FromDateTime(startDate.AddDays(90));

        // Act
        var result = account.GetDailyPrice(
            DateOnly.FromDateTime(startDate), 
            targetDate, 
            [bondDetails]);

        // Assert
        var day0 = result[DateOnly.FromDateTime(startDate)];
        var day29 = result[DateOnly.FromDateTime(startDate.AddDays(29))];
        var day30 = result[DateOnly.FromDateTime(startDate.AddDays(30))];
        var day60 = result[DateOnly.FromDateTime(startDate.AddDays(60))];
        var day90 = result[targetDate];

        // Day 0: 1000
        Assert.Equal(1000m, day0);
        
        // Day 29: ~1000 with some growth
        Assert.True(day29 > 1000m && day29 < 1010m);
        
        // Day 30: Should jump to ~1500 + accumulated interest (new entry added)
        Assert.True(day30 > 1500m && day30 < 1520m);
        
        // Day 60: Should jump again (third entry)
        Assert.True(day60 > 1750m && day60 < 1780m);
        
        // Day 90: All entries accumulating
        Assert.True(day90 > 1760m, $"Expected final value > 1760, got {day90}");
    }

    [Fact]
    public async Task GetDailyPrice_EmptyAccount_ShouldReturnEmpty()
    {
        // Arrange
        var startDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var account = new BondAccount(1, 1, "Empty Account", AccountLabel.Other);

        var targetDate = DateOnly.FromDateTime(startDate.AddDays(10));

        // Act
        var result = account.GetDailyPrice(
            DateOnly.FromDateTime(startDate), 
            targetDate, 
            []);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDailyPrice_MissingBondDetails_ShouldThrowException()
    {
        // Arrange - Entry references bond ID 1 but no details provided
        var postingDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var entry = new BondAccountEntry(1, 1, postingDate, 0, 1000m, 1);

        var account = new BondAccount(1, 1, "Test Account", AccountLabel.Other);
        account.Add(entry);

        var targetDate = DateOnly.FromDateTime(postingDate.AddDays(10));

        // Act & Assert - Should throw ArgumentException when bond details are missing
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () => 
            account.GetDailyPrice(
                DateOnly.FromDateTime(postingDate), 
                targetDate, 
                [])); // No bond details provided
        
        Assert.Contains("BondDetails", exception.Message);
    }
}