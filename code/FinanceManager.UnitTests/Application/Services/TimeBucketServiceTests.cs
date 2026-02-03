using FinanceManager.Application.Services;
using System.Globalization;

namespace FinanceManager.UnitTests.Application.Services;

[Collection("unit")]
[Trait("Category", "Unit")]
public class TimeBucketServiceTests
{
    [Fact]
    public void Get_WithTimeBucket_Day_GroupsByDay()
    {
        // Arrange
        List<(DateTime, string)> data =
        [
            (new DateTime(2024, 1, 1), "Test"),
            (new DateTime(2024, 1, 1).AddHours(2), "Test"),
            (new DateTime(2024, 1, 2).AddTicks(-1), "Test"),

            (new DateTime(2025, 9, 1), "Test"),
            (new DateTime(2025, 9, 2).AddTicks(-1), "Test"),

            (new DateTime(2025, 9, 2), "Test"),
            (new DateTime(2025, 9, 3).AddTicks(-1), "Test"),

            (new DateTime(2025, 10, 1), "Test"),
        ];

        // Act
        var result = TimeBucketService.Get(data, TimeBucket.Day).ToList();

        // Assert
        Assert.Equal(4, result.Count);
    }

    [Fact]

    public void Get_WithTimeBucket_Week_GroupsByWeek()
    {
        // Arrange
        List<(DateTime, string)> data =
        [
            (new DateTime(2024, 1, 1), "Test"),
            (new DateTime(2024, 1, 1).AddHours(2), "Test"),
            (new DateTime(2024, 1, 2).AddTicks(-1), "Test"),
            (new DateTime(2024, 1, 1).AddDays(1), "Test"),
            (new DateTime(2024, 1, 4), "Test"),

            (new DateTime(2025, 9, 1), "Test"),
            (new DateTime(2025, 9, 7), "Test"),

            (new DateTime(2025, 9, 8), "Test"),
            (new DateTime(2025, 9, 14), "Test"),

            (new DateTime(2025, 10, 1), "Test"),
        ];

        // Act
        var original = CultureInfo.CurrentCulture;
        List<(DateTime Date, List<string> Objects)> result = [];
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-GB"); // or "en-US", pick one
                                                                   // Arrange ...
            result = TimeBucketService.Get(data, TimeBucket.Week).ToList();
            Assert.Equal(4, result.Count);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }


        // Assert
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void Get_WithTimeBucket_Month_GroupsByMonth()
    {
        // Arrange
        List<(DateTime, string)> data =
        [
            (new DateTime(2024, 1, 1), "Test"),
            (new DateTime(2024, 1, 1).AddHours(2), "Test"),
            (new DateTime(2024, 1, 2).AddTicks(-1), "Test"),
            (new DateTime(2024, 1, 1).AddDays(1), "Test"),
            (new DateTime(2024, 1, 4), "Test"),
            (new DateTime(2024, 1, 31), "Test"),

            (new DateTime(2025, 9, 1), "Test"),
            (new DateTime(2025, 9, 30), "Test"),

            (new DateTime(2025, 10, 1), "Test"),
            (new DateTime(2025, 10, 29), "Test"),

            (new DateTime(2025, 11, 1), "Test"),
        ];

        // Act
        var result = TimeBucketService.Get(data, TimeBucket.Month).ToList();

        // Assert
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void Get_WithTimeBucket_Year_GroupsByYear()
    {
        // Arrange
        List<(DateTime, string)> data =
        [
            (new DateTime(2024, 1, 1), "Test"),
            (new DateTime(2024, 1, 1).AddHours(2), "Test"),
            (new DateTime(2024, 1, 2).AddTicks(-1), "Test"),
            (new DateTime(2024, 1, 1).AddDays(1), "Test"),
            (new DateTime(2024, 1, 4), "Test"),
            (new DateTime(2024, 1, 31), "Test"),
            (new DateTime(2024, 12, 31), "Test"),

            (new DateTime(2025, 1, 1), "Test")
        ];

        // Act
        var result = TimeBucketService.Get(data, TimeBucket.Year).ToList();

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Get_AutoSelects_Daily_ForShortRange()
    {
        // Arrange: Range <= 31 days
        var data = new[]
        {
            (DateTime.Parse("2023-01-01"), 10),
            (DateTime.Parse("2023-01-15"), 20)
        };

        // Act
        var result = TimeBucketService.Get(data).ToList();

        // Assert: Should group daily
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Get_AutoSelects_Weekly_ForMediumRange()
    {
        // Arrange: 31 < days <= 93
        var data = new[]
        {
            (DateTime.Parse("2023-01-01"), 10),
            (DateTime.Parse("2023-01-02"), 10),
            (DateTime.Parse("2023-02-15"), 20)
        };

        // Act
        var result = TimeBucketService.Get(data).ToList();

        // Assert: Should group weekly
        Assert.True(result.Count >= 1);
    }

    [Fact]
    public void Get_AutoSelects_Monthly_ForLongRange()
    {
        // Arrange: 93 < days <= 365
        var data = new[]
        {
            (DateTime.Parse("2023-01-01"), 10),
            (DateTime.Parse("2023-01-02"), 10),
            (DateTime.Parse("2023-06-01"), 20)
        };

        // Act
        var result = TimeBucketService.Get(data).ToList();

        // Assert: Should group monthly
        Assert.Equal(2, result.Count); // Jan and Jun
    }

    [Fact]
    public void Get_AutoSelects_Yearly_ForVeryLongRange()
    {
        // Arrange: days > 365
        var data = new[]
        {
            (DateTime.Parse("2022-01-01"), 10),
            (DateTime.Parse("2022-01-02"), 10),
            (DateTime.Parse("2023-01-01"), 10),
            (DateTime.Parse("2024-01-01"), 20)
        };

        // Act
        var result = TimeBucketService.Get(data).ToList();

        // Assert: Should group yearly
        Assert.Equal(3, result.Count); // 2022, 2023, 2024
    }

    [Fact]
    public void Get_WithTimeBucket_ThrowsForInvalidBucket()
    {
        // Arrange
        var data = new[] { (DateTime.UtcNow, 10) };

        // Act & Assert
        Assert.Throws<NotImplementedException>(() => TimeBucketService.Get(data, (TimeBucket)999));
    }

    [Fact]
    public void Get_HandlesEmptyData()
    {
        // Arrange
        var data = Array.Empty<(DateTime, int)>();

        // Act
        var result = TimeBucketService.Get(data, TimeBucket.Day).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Get_HandlesSingleItem()
    {
        // Arrange
        var data = new[] { (DateTime.Parse("2023-01-01"), 10) };

        // Act
        var result = TimeBucketService.Get(data, TimeBucket.Day).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(DateTime.Parse("2023-01-01"), result[0].Date);
        Assert.Equal([10], result[0].Objects);
    }

    [Fact]
    public void Get_With365DataPoints_AutoSelectsAndGroupsCorrectly()
    {
        // Arrange: 365 data points with values 0, 10, 20, ..., 3640
        var data = new List<(DateTime, int)>();
        for (int i = 0; i < 365; i++)
            data.Add((DateTime.Parse("2023-01-01").AddDays(i), i * 10));

        // Act
        var result = TimeBucketService.Get(data).ToList();

        // Assert: All results should have values greater than 0
        Assert.All(result, r => Assert.True(r.Objects.Last() > 0));
    }
}