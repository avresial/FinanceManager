using FinanceManager.Domain.Entities.Bonds;

namespace FinanceManager.UnitTests.Domain.Entities.Bonds;

[Collection("Domain")]
[Trait("Category", "Unit")]
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

    [Fact]
    public void GetPrice_LeapYear_ShouldHandleCapitalizationCorrectly()
    {
        // Arrange - Start in leap year 2024
        var postingDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var entry = new BondAccountEntry(1, 1, postingDate, 1000m, 1000m, 1);

        var calculationMethod = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = FinanceManager.Domain.Enums.DateOperator.UntilDate,
            DateValue = "2026-01-01",
            Rate = 0.0365m // 3.65%
        };

        var bondDetails = new BondDetails(
            "Test Bond",
            "Issuer",
            DateOnly.FromDateTime(postingDate),
            DateOnly.FromDateTime(postingDate.AddYears(3)),
            [calculationMethod]
        );

        // Target date is after 365 days (should capitalize even though 2024 is leap year)
        var targetDate = DateOnly.FromDateTime(postingDate.AddDays(365));

        // Act
        var result = entry.GetPrice(targetDate, bondDetails);

        // Assert - Should capitalize after exactly 365 days
        Assert.Equal(366, result.Count); // 366 days (0 through 365)
        var capitalizedValue = result[DateOnly.FromDateTime(postingDate.AddDays(365))];

        // After 365 days at 3.65% daily rate, should be close to 1036.5 (with compound on day 365)
        Assert.True(capitalizedValue > 1036m && capitalizedValue < 1037m,
            $"Expected capitalized value around 1036.5, got {capitalizedValue}");
    }

    [Fact]
    public void GetPrice_RateChangeMidPeriod_ShouldUseCorrectRate()
    {
        // Arrange
        var postingDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var entry = new BondAccountEntry(1, 1, postingDate, 1000m, 1000m, 1);

        // First rate applies until July 1, 2023
        var calculationMethod1 = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = FinanceManager.Domain.Enums.DateOperator.UntilDate,
            DateValue = "2023-07-01",
            Rate = 0.0365m // 3.65%
        };

        // Second rate applies until end of period
        var calculationMethod2 = new BondCalculationMethod
        {
            Id = 2,
            DateOperator = FinanceManager.Domain.Enums.DateOperator.UntilDate,
            DateValue = "2024-01-01",
            Rate = 0.0500m // 5%
        };

        var bondDetails = new BondDetails(
            "Test Bond",
            "Issuer",
            DateOnly.FromDateTime(postingDate),
            DateOnly.FromDateTime(postingDate.AddYears(1)),
            [calculationMethod1, calculationMethod2]
        );

        var beforeRateChange = DateOnly.FromDateTime(new DateTime(2023, 6, 30, 0, 0, 0, DateTimeKind.Utc));
        var afterRateChange = DateOnly.FromDateTime(new DateTime(2023, 7, 2, 0, 0, 0, DateTimeKind.Utc));

        // Act
        var result = entry.GetPrice(afterRateChange, bondDetails);

        // Assert - Should have values for all days
        Assert.True(result.ContainsKey(beforeRateChange));
        Assert.True(result.ContainsKey(afterRateChange));

        // Value should continue to grow (no drop at rate change)
        Assert.True(result[afterRateChange] > result[beforeRateChange]);
    }

    [Fact]
    public void GetPrice_MultiYearPeriod_ShouldMaintainPrecision()
    {
        // Arrange - Test 5 year period to verify no significant rounding errors
        var postingDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var entry = new BondAccountEntry(1, 1, postingDate, 10000m, 10000m, 1);

        var calculationMethod = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = FinanceManager.Domain.Enums.DateOperator.UntilDate,
            DateValue = "2026-01-01",
            Rate = 0.05m // 5%
        };

        var bondDetails = new BondDetails(
            "Test Bond",
            "Issuer",
            DateOnly.FromDateTime(postingDate),
            DateOnly.FromDateTime(postingDate.AddYears(5)),
            [calculationMethod]
        );

        var targetDate = DateOnly.FromDateTime(postingDate.AddYears(5));

        // Act
        var result = entry.GetPrice(targetDate, bondDetails);

        // Assert - After 5 years with 5% annual compound interest
        // Expected: 10000 * (1.05)^5 ≈ 12762.82
        var finalValue = result[targetDate];
        Assert.True(finalValue > 12750m && finalValue < 12775m,
            $"Expected value around 12762, got {finalValue}");

        // Verify monotonically increasing (no calculation errors causing drops)
        var dates = result.Keys.OrderBy(k => k).ToList();
        for (int i = 1; i < dates.Count; i++)
        {
            Assert.True(result[dates[i]] >= result[dates[i - 1]],
                $"Value decreased from {result[dates[i - 1]]} to {result[dates[i]]} at day {i}");
        }
    }

    [Fact]
    public void GetPrice_NegativeValueChange_ShouldCalculateFromNegativeCapital()
    {
        // Arrange - Bond redemption (selling/withdrawing)
        var postingDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var entry = new BondAccountEntry(1, 1, postingDate, 0m, -500m, 1); // Negative value change

        var calculationMethod = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = FinanceManager.Domain.Enums.DateOperator.UntilDate,
            DateValue = "2024-01-01",
            Rate = 0.0365m
        };

        var bondDetails = new BondDetails(
            "Test Bond",
            "Issuer",
            DateOnly.FromDateTime(postingDate),
            DateOnly.FromDateTime(postingDate.AddYears(1)),
            [calculationMethod]
        );

        var targetDate = DateOnly.FromDateTime(postingDate.AddDays(10));

        // Act
        var result = entry.GetPrice(targetDate, bondDetails);

        // Assert - Should grow from -500 (becoming more negative)
        Assert.Equal(-500m, result[DateOnly.FromDateTime(postingDate)]);
        var dayOneValue = result[DateOnly.FromDateTime(postingDate.AddDays(1))];

        // Negative capital should grow more negative with interest
        Assert.True(dayOneValue < -500m, $"Expected value more negative than -500, got {dayOneValue}");
    }

    [Fact]
    public void GetPrice_SameDayEntry_ShouldReturnSingleDay()
    {
        // Arrange - Entry where target date equals posting date
        var postingDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var entry = new BondAccountEntry(1, 1, postingDate, 1000m, 1000m, 1);

        var calculationMethod = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = FinanceManager.Domain.Enums.DateOperator.UntilDate,
            DateValue = "2024-01-01",
            Rate = 0.0365m
        };

        var bondDetails = new BondDetails(
            "Test Bond",
            "Issuer",
            DateOnly.FromDateTime(postingDate),
            DateOnly.FromDateTime(postingDate.AddYears(1)),
            [calculationMethod]
        );

        var targetDate = DateOnly.FromDateTime(postingDate);

        // Act
        var result = entry.GetPrice(targetDate, bondDetails);

        // Assert - Should only contain posting date
        Assert.Single(result);
        Assert.Equal(1000m, result[DateOnly.FromDateTime(postingDate)]);
    }

    [Fact]
    public void GetPrice_ZeroInitialValue_ShouldCalculateFromValueChange()
    {
        // Arrange
        var postingDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var entry = new BondAccountEntry(1, 1, postingDate, 0m, 2000m, 1);

        var calculationMethod = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = FinanceManager.Domain.Enums.DateOperator.UntilDate,
            DateValue = "2024-01-01",
            Rate = 0.04m // 4%
        };

        var bondDetails = new BondDetails(
            "Test Bond",
            "Issuer",
            DateOnly.FromDateTime(postingDate),
            DateOnly.FromDateTime(postingDate.AddYears(1)),
            [calculationMethod]
        );

        var targetDate = DateOnly.FromDateTime(postingDate.AddDays(100));

        // Act
        var result = entry.GetPrice(targetDate, bondDetails);

        // Assert - Should start from ValueChange (2000) not Value (0)
        Assert.Equal(2000m, result[DateOnly.FromDateTime(postingDate)]);
        Assert.True(result[targetDate] > 2000m, "Value should grow from initial ValueChange");
    }

    [Fact]
    public void GetPrice_NoActiveCalculationMethod_ShouldSkipDays()
    {
        // Arrange - Calculation method that expires before target date
        var postingDate = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var entry = new BondAccountEntry(1, 1, postingDate, 1000m, 1000m, 1);

        var calculationMethod = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = FinanceManager.Domain.Enums.DateOperator.UntilDate,
            DateValue = "2023-01-05", // Expires after 5 days
            Rate = 0.0365m
        };

        var bondDetails = new BondDetails(
            "Test Bond",
            "Issuer",
            DateOnly.FromDateTime(postingDate),
            DateOnly.FromDateTime(postingDate.AddYears(1)),
            [calculationMethod]
        );

        var targetDate = DateOnly.FromDateTime(postingDate.AddDays(10));

        // Act
        var result = entry.GetPrice(targetDate, bondDetails);

        // Assert - Should have entries up to when calculation method is active
        // After calculation method expires (day 5), no more entries should be added
        Assert.True(result.Count < 6, $"Expected at most 5 entries (days 0-5), got {result.Count}");
        Assert.Contains(DateOnly.FromDateTime(postingDate), result.Keys);
        Assert.Contains(DateOnly.FromDateTime(postingDate.AddDays(4)), result.Keys);

        // Days after expiry should not be in the result
        Assert.DoesNotContain(DateOnly.FromDateTime(postingDate.AddDays(6)), result.Keys);
    }
}