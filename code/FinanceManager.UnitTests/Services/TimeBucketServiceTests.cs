using FinanceManager.Application.Services;

namespace FinanceManager.UnitTests.Services;
public class TimeBucketServiceTests
{
    [Fact]
    public async Task Get_Daily()
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
        var result = TimeBucketService.Get<string>(data, TimeBucket.Day).ToList();

        // Assert
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public async Task Get_Weekly()
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
        var result = TimeBucketService.Get<string>(data, TimeBucket.Week).ToList();

        // Assert
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public async Task Get_Monthly()
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
        var result = TimeBucketService.Get<string>(data, TimeBucket.Month).ToList();

        // Assert
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public async Task Get_Yearly()
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
        var result = TimeBucketService.Get<string>(data, TimeBucket.Year).ToList();

        // Assert
        Assert.Equal(2, result.Count);
    }
}
