using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Shared;
using System.Diagnostics;

namespace FinanceManager.Tests.Unit.Domain.Entities.Bonds;

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
            [calculationMethod],
            unitValue: 1m
        )
        { Id = 1 };

        // Act
        var result = account.GetDailyPrice(DateOnly.FromDateTime(postingDate), DateOnly.FromDateTime(endDate), [bondDetails]);

        // Assert
        Assert.Equal(5, result.Count);
        Assert.Equal(100.01m, result[DateOnly.FromDateTime(postingDate.AddDays(1))]);
        Assert.Equal(200m, result[DateOnly.FromDateTime(endDate)], 2);
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
            [calculationMethod1],
            unitValue: 1m
        )
        { Id = 1 };

        var bondDetails2 = new BondDetails(
            "Bond 5%",
            "Issuer B",
            DateOnly.FromDateTime(postingDate),
            DateOnly.FromDateTime(postingDate.AddYears(1)),
            [calculationMethod2],
            unitValue: 1m
        )
        { Id = 2 };

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
            [calculationMethod],
            unitValue: 1m
        )
        { Id = 1 };

        var targetDate = DateOnly.FromDateTime(postingDate.AddDays(10));

        // Act
        var result = account.GetDailyPrice(
            DateOnly.FromDateTime(postingDate),
            targetDate,
            [bondDetails]);

        // Assert - Zero balance is omitted from the result set.
        Assert.Empty(result);
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
            [calculationMethod],
            unitValue: 1m
        )
        { Id = 1 };

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

        // Day 30: New entry establishes the cumulative units for that day.
        Assert.Equal(1500m, day30);

        // Day 60: Third entry establishes the new cumulative units.
        Assert.Equal(1750m, day60);

        // Day 90: All entries accumulating
        Assert.True(day90 > 1755m, $"Expected final value > 1755, got {day90}");
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

        // Act & Assert - Should throw when bond details are missing
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            account.GetDailyPrice(
                DateOnly.FromDateTime(postingDate),
                targetDate,
                [])); // No bond details provided

        Assert.Contains("Bond valuation requires details", exception.Message);
    }

    [Fact]
    public void GetDailyPrice_ShouldIncludeBondsFromNextOlderEntries()
    {
        var startDate = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddDays(2);

        var carriedBondEntry = new BondAccountEntry(1, 1, startDate.AddDays(-5), 10m, 10m, 1);
        var currentBondEntry = new BondAccountEntry(1, 2, startDate, 5m, 5m, 2);

        var account = new BondAccount(
            1,
            1,
            "Test Account",
            [currentBondEntry],
            AccountLabel.Other,
            new Dictionary<int, BondAccountEntry> { [1] = carriedBondEntry });

        var firstBondDetails = new BondDetails(
            "Bond A",
            "Issuer A",
            DateOnly.FromDateTime(startDate.AddYears(-1)),
            DateOnly.FromDateTime(endDate.AddYears(1)),
            [new BondCalculationMethod { Id = 1, DateOperator = DateOperator.UntilDate, DateValue = endDate.AddYears(1).ToString("yyyy-MM-dd"), Rate = 0m }],
            unitValue: 1m)
        { Id = 1 };

        var secondBondDetails = new BondDetails(
            "Bond B",
            "Issuer B",
            DateOnly.FromDateTime(startDate),
            DateOnly.FromDateTime(endDate.AddYears(1)),
            [new BondCalculationMethod { Id = 2, DateOperator = DateOperator.UntilDate, DateValue = endDate.AddYears(1).ToString("yyyy-MM-dd"), Rate = 0m }],
            unitValue: 1m)
        { Id = 2 };

        var result = account.GetDailyPrice(
            DateOnly.FromDateTime(startDate),
            DateOnly.FromDateTime(endDate),
            [firstBondDetails, secondBondDetails]);

        Assert.Equal(3, result.Count);
        Assert.All(result.Values, value => Assert.Equal(15m, value));
    }

    [Fact]
    public void GetDailyPrice_FiveYearHistory_MatchesReferenceCalculation()
    {
        var postingDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var start = DateOnly.FromDateTime(postingDate);
        var end = DateOnly.FromDateTime(postingDate.AddYears(5));

        var account = new BondAccount(1, 1, "Test Account", AccountLabel.Other);
        account.Add(new BondAccountEntry(1, 1, postingDate, 0, 1000m, 1));
        account.Add(new BondAccountEntry(1, 2, postingDate.AddYears(1), 0, 500m, 1));
        account.Add(new BondAccountEntry(1, 3, postingDate.AddYears(2), 0, -500m, 1));
        account.Add(new BondAccountEntry(1, 4, postingDate.AddYears(3).AddDays(100), 0, 200m, 1));

        var method1 = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2022-01-01",
            Rate = 0.0365m
        };
        var method2 = new BondCalculationMethod
        {
            Id = 2,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2026-01-01",
            Rate = 0.05m
        };

        var bondDetails = new BondDetails(
            "Test Bond",
            "Issuer",
            start,
            end.AddYears(1),
            [method1, method2],
            unitValue: 1m)
        { Id = 1 };

        var result = account.GetDailyPrice(start, end, [bondDetails]);
        var reference = GetDailyPriceReference(account, start, end, [bondDetails]);

        AssertDailyPriceEqual(reference, result);
    }

    [Fact]
    public void GetDailyPrice_ComplexScenario_MatchesReferenceCalculation()
    {
        var baseDate = new DateTime(2023, 3, 10, 0, 0, 0, DateTimeKind.Utc);
        var start = DateOnly.FromDateTime(baseDate.AddDays(-18));
        var end = DateOnly.FromDateTime(baseDate.AddDays(120));

        var account = new BondAccount(1, 1, "Test Account", AccountLabel.Other);
        account.Add(new BondAccountEntry(1, 1, baseDate, 0, 1000m, 1));
        account.Add(new BondAccountEntry(1, 2, baseDate.AddHours(15), 0, 250m, 1));
        account.Add(new BondAccountEntry(1, 3, baseDate.AddDays(30), 0, 100m, 1));
        account.Add(new BondAccountEntry(1, 4, baseDate.AddDays(30), 0, -100m, 1));
        account.Add(new BondAccountEntry(1, 5, baseDate.AddDays(44), 0, 300m, 1));
        account.Add(new BondAccountEntry(1, 8, baseDate.AddDays(115), 0, -1550m, 1));
        account.Add(new BondAccountEntry(1, 6, baseDate.AddDays(10), 0, 700m, 2));
        account.Add(new BondAccountEntry(1, 7, baseDate.AddDays(80), 0, -700m, 2));

        var carriedEntry = new BondAccountEntry(1, 9, baseDate.AddDays(-13), 150m, 150m, 3);
        account.NextOlderEntries[3] = carriedEntry;

        var method1 = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2023-05-01",
            Rate = 0.0365m
        };
        var method2 = new BondCalculationMethod
        {
            Id = 2,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2031-01-01",
            Rate = 0.05m
        };
        var method3 = new BondCalculationMethod
        {
            Id = 3,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2023-06-01",
            Rate = 0.0365m
        };
        var method4 = new BondCalculationMethod
        {
            Id = 4,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2023-04-15",
            Rate = 0.0365m
        };

        var bond1 = new BondDetails("Bond 1", "Issuer A", start, end.AddYears(1), [method1, method2], unitValue: 1m) { Id = 1 };
        var bond2 = new BondDetails("Bond 2", "Issuer B", start, end.AddYears(1), [method3], unitValue: 1m) { Id = 2 };
        var bond3 = new BondDetails("Bond 3", "Issuer C", start, end.AddYears(1), [method4], unitValue: 1m) { Id = 3 };

        var result = account.GetDailyPrice(start, end, [bond1, bond2, bond3]);
        var reference = GetDailyPriceReference(account, start, end, [bond1, bond2, bond3]);

        AssertDailyPriceEqual(reference, result);

        Assert.Equal(128, result.Count);
        Assert.Equal(150.06m, result[DateOnly.FromDateTime(baseDate.AddDays(-9))]);
        Assert.DoesNotContain(DateOnly.FromDateTime(baseDate.AddDays(-18)), result.Keys);
        Assert.Contains(DateOnly.FromDateTime(baseDate.AddDays(-13)), result.Keys);
        Assert.DoesNotContain(DateOnly.FromDateTime(baseDate.AddDays(115)), result.Keys);
    }

    [Theory]
    [InlineData(1, 367, 1000)]
    [InlineData(5, 1828, 1500)]
    [InlineData(10, 3654, 2500)]
    public void GetDailyPrice_LongHistories_CompleteWithinBudget(int years, int expectedDays, int budgetMs)
    {
        var postingDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var start = DateOnly.FromDateTime(postingDate);
        var end = DateOnly.FromDateTime(postingDate.AddYears(years));

        var account = new BondAccount(1, 1, "Test Account", AccountLabel.Other);
        account.Add(new BondAccountEntry(1, 1, postingDate, 0, 1000m, 1));

        var calculationMethod = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2031-01-01",
            Rate = 0.0365m
        };

        var bondDetails = new BondDetails(
            "Test Bond",
            "Issuer",
            start,
            end.AddYears(1),
            [calculationMethod],
            unitValue: 1m)
        { Id = 1 };

        var stopwatch = Stopwatch.StartNew();
        var result = account.GetDailyPrice(start, end, [bondDetails]);
        stopwatch.Stop();

        Assert.Equal(expectedDays, result.Count);
        Assert.InRange(stopwatch.ElapsedMilliseconds, 0, budgetMs);
    }

    private static void AssertDailyPriceEqual(Dictionary<DateOnly, decimal> expected, Dictionary<DateOnly, decimal> actual)
    {
        var expectedKeys = expected.Keys.ToList();
        var actualKeys = actual.Keys.ToList();

        Assert.True(
            expectedKeys.SequenceEqual(actualKeys),
            $"Expected keys [{string.Join(", ", expectedKeys.Select(k => k.ToString("yyyy-MM-dd")))}] but got [{string.Join(", ", actualKeys.Select(k => k.ToString("yyyy-MM-dd")))}].");

        foreach (var key in expectedKeys)
        {
            Assert.True(actual.TryGetValue(key, out var actualValue), $"Missing key {key:yyyy-MM-dd}.");
            Assert.True(actualValue == expected[key], $"Value mismatch on {key:yyyy-MM-dd}: expected {expected[key]}, got {actualValue}.");
        }
    }

    private static Dictionary<DateOnly, decimal> GetDailyPriceReference(BondAccount account, DateOnly start, DateOnly end, List<BondDetails> bondDetails)
    {
        var result = new Dictionary<DateOnly, decimal>();
        if (account.Entries is null || start > end) return result;

        var detailsById = bondDetails.ToDictionary(x => x.Id);
        var detailsIds = account.Entries.Select(e => e.BondDetailsId)
            .Concat(account.NextOlderEntries.Keys)
            .Distinct()
            .ToList();

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            decimal total = 0;
            foreach (var detailId in detailsIds)
            {
                var currentEntry = account.GetThisOrNextOlder(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), detailId);
                if (currentEntry is null) continue;

                total += currentEntry.GetPriceAt(date, detailsById[detailId]);
            }

            if (total != 0)
                result[date] = total;
        }

        return result;
    }
}