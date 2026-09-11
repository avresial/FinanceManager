using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Shared;
using System.Diagnostics;
using Xunit;

namespace FinanceManager.Tests.Unit.Domain.Entities.Bonds;

[Collection("Domain")]
[Trait("Category", "Unit")]
public class BondAccountAccrualPerformanceTests(ITestOutputHelper output)
{
    [Fact]
    public void GetDailyPrice_OneYearHistory_MultipleInstruments_PreservesAccrualAndMeasuresPerformance()
    {
        // Arrange: 1-year history (365 days in 2023) with two distinct bond instruments
        var startDate = new DateOnly(2023, 1, 1);
        var endDate = new DateOnly(2023, 12, 31);
        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var calculationMethod1 = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2025-01-01",
            Rate = 0.0365m // 3.65% -> 0.01 daily per 100
        };

        var calculationMethod2 = new BondCalculationMethod
        {
            Id = 2,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2025-01-01",
            Rate = 0.05m // 5.00%
        };

        var bondDetails1 = new BondDetails(
            "Bond 1 (3.65%)",
            "Treasury",
            startDate,
            endDate.AddYears(2),
            [calculationMethod1],
            unitValue: 1m
        )
        { Id = 1 };

        var bondDetails2 = new BondDetails(
            "Bond 2 (5.00%)",
            "Treasury",
            startDate,
            endDate.AddYears(2),
            [calculationMethod2],
            unitValue: 1m
        )
        { Id = 2 };

        var entry1 = new BondAccountEntry(1, 1, startDateTime, 0, 1000m, 1);
        var entry2 = new BondAccountEntry(1, 2, startDateTime, 0, 2000m, 2);

        var account = new BondAccount(1, 1, "Performance Test Account", AccountLabel.Other);
        account.Add(entry1);
        account.Add(entry2);

        List<BondDetails> detailsList = [bondDetails1, bondDetails2];

        // Act: Measure execution time and heap allocation for 1-year daily pricing
        var startAllocated = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        var dailyPrices = account.GetDailyPrice(startDate, endDate, detailsList);
        stopwatch.Stop();
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - startAllocated;

        output.WriteLine($"[1-Year History] Elapsed: {stopwatch.ElapsedMilliseconds} ms, Allocated: {allocatedBytes:N0} bytes, Days: {dailyPrices.Count}");

        // Assert: Explicit budgets catch accidental regressions while leaving headroom for CI variance.
        Assert.InRange(stopwatch.ElapsedMilliseconds, 0, 1_000);
        Assert.InRange(allocatedBytes, 0, 5_000_000);

        // Assert: Deterministic daily count and values
        var expectedDays = endDate.DayNumber - startDate.DayNumber + 1;
        Assert.Equal(expectedDays, dailyPrices.Count);

        // Day 0: exactly initial capital sum (1000 + 2000 = 3000)
        Assert.Equal(3000m, dailyPrices[startDate]);

        // Day 1: 1000 + Round(1000 * 0.0365 / 365, 5) + 2000 + Round(2000 * 0.05 / 365, 5)
        // Bond 1: 1000 + 0.10000 = 1000.10
        // Bond 2: 2000 + 0.27397 = 2000.27397
        // Total: 3000.37397
        Assert.Equal(3000.37397m, dailyPrices[startDate.AddDays(1)]);

        // Verify equivalence against direct point-in-time calculation at monthly intervals
        for (var date = startDate; date <= endDate; date = date.AddMonths(1))
        {
            var expectedPrice1 = entry1.GetPriceAt(date, bondDetails1);
            var expectedPrice2 = entry2.GetPriceAt(date, bondDetails2);
            Assert.Equal(expectedPrice1 + expectedPrice2, dailyPrices[date]);
        }
    }

    [Fact]
    public void GetDailyPrice_FiveYearHistory_MultipleInstrumentsAndStaggeredEntries_MeasuresPerformanceAndMonotonicity()
    {
        // Arrange: 5-year history (2020-01-01 to 2024-12-31, 1827 days including leap years 2020 & 2024)
        var startDate = new DateOnly(2020, 1, 1);
        var endDate = new DateOnly(2024, 12, 31);
        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Bond 1 has a rate change after 2 years
        var method1A = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2022-01-01",
            Rate = 0.04m // 4.0%
        };
        var method1B = new BondCalculationMethod
        {
            Id = 2,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2026-01-01",
            Rate = 0.06m // 6.0%
        };

        // Bond 2 has a fixed 5.0% rate
        var method2 = new BondCalculationMethod
        {
            Id = 3,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2026-01-01",
            Rate = 0.05m
        };

        var bondDetails1 = new BondDetails(
            "Bond 1 (Floating/Stepped)",
            "Issuer A",
            startDate,
            endDate.AddYears(1),
            [method1A, method1B],
            unitValue: 1m
        )
        { Id = 1 };

        var bondDetails2 = new BondDetails(
            "Bond 2 (Fixed 5%)",
            "Issuer B",
            startDate,
            endDate.AddYears(1),
            [method2],
            unitValue: 1m
        )
        { Id = 2 };

        var account = new BondAccount(1, 1, "5-Year Performance Account", AccountLabel.Other);

        // Initial purchases at start
        var entry1 = new BondAccountEntry(1, 1, startDateTime, 0, 1000m, 1);
        account.Add(entry1);

        // Staggered purchases: Bond 2 bought at year 1, Bond 1 topped up at year 2.5
        var entry2 = new BondAccountEntry(1, 2, new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc), 0, 2000m, 2);
        account.Add(entry2);

        var entry3 = new BondAccountEntry(1, 3, new DateTime(2022, 7, 1, 0, 0, 0, DateTimeKind.Utc), 0, 500m, 1);
        account.Add(entry3);

        List<BondDetails> detailsList = [bondDetails1, bondDetails2];

        // Act: Measure execution time and heap allocation for 5-year daily pricing
        var startAllocated = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        var dailyPrices = account.GetDailyPrice(startDate, endDate, detailsList);
        stopwatch.Stop();
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - startAllocated;

        output.WriteLine($"[5-Year History] Elapsed: {stopwatch.ElapsedMilliseconds} ms, Allocated: {allocatedBytes:N0} bytes, Days: {dailyPrices.Count}");

        // Assert: Explicit budgets catch accidental regressions while leaving headroom for CI variance.
        Assert.InRange(stopwatch.ElapsedMilliseconds, 0, 2_500);
        Assert.InRange(allocatedBytes, 0, 15_000_000);

        var expectedDays = endDate.DayNumber - startDate.DayNumber + 1;
        Assert.Equal(expectedDays, dailyPrices.Count);

        // Verify values are strictly positive and monotonically non-decreasing
        var orderedKeys = dailyPrices.Keys.OrderBy(k => k).ToList();
        for (int i = 1; i < orderedKeys.Count; i++)
        {
            Assert.True(
                dailyPrices[orderedKeys[i]] >= dailyPrices[orderedKeys[i - 1]],
                $"Price decreased from {dailyPrices[orderedKeys[i - 1]]} on {orderedKeys[i - 1]} to {dailyPrices[orderedKeys[i]]} on {orderedKeys[i]}."
            );
        }

        // Verify equivalence with independent point-in-time calculation across key milestones
        DateOnly[] testCheckpoints =
        [
            startDate,
            new DateOnly(2020, 12, 31),
            new DateOnly(2021, 1, 1),   // Bond 2 addition
            new DateOnly(2022, 1, 1),   // Rate change boundary
            new DateOnly(2022, 7, 1),   // Bond 1 top-up
            new DateOnly(2023, 6, 15),
            endDate
        ];

        foreach (var checkpoint in testCheckpoints)
        {
            var dt = checkpoint.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            decimal expectedSum = 0;

            var e1 = account.GetThisOrNextOlder(dt, 1);
            if (e1 is not null)
                expectedSum += e1.GetPriceAt(checkpoint, bondDetails1);

            var e2 = account.GetThisOrNextOlder(dt, 2);
            if (e2 is not null)
                expectedSum += e2.GetPriceAt(checkpoint, bondDetails2);

            Assert.Equal(expectedSum, dailyPrices[checkpoint]);
        }
    }

    [Fact]
    public void GetDailyPrice_TenYearHistory_LongHorizonScaling_MaintainsPrecisionAndMeasuresThroughput()
    {
        // Arrange: 10-year horizon (2020-01-01 to 2029-12-31, 3653 days) with 3 instruments
        var startDate = new DateOnly(2020, 1, 1);
        var endDate = new DateOnly(2029, 12, 31);
        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var bondDetails1 = new BondDetails(
            "10-Yr Inflation Bond",
            "Treasury",
            new DateOnly(2019, 1, 1),
            new DateOnly(2035, 1, 1),
            [new BondCalculationMethod { Id = 1, DateOperator = DateOperator.UntilDate, DateValue = "2035-01-01", Rate = 0.035m }],
            unitValue: 1m
        )
        { Id = 1 };

        var bondDetails2 = new BondDetails(
            "10-Yr Fixed Bond",
            "Treasury",
            new DateOnly(2020, 1, 1),
            new DateOnly(2035, 1, 1),
            [new BondCalculationMethod { Id = 2, DateOperator = DateOperator.UntilDate, DateValue = "2035-01-01", Rate = 0.045m }],
            unitValue: 1m
        )
        { Id = 2 };

        var bondDetails3 = new BondDetails(
            "10-Yr High-Yield Bond",
            "Corporate",
            new DateOnly(2020, 1, 1),
            new DateOnly(2035, 1, 1),
            [new BondCalculationMethod { Id = 3, DateOperator = DateOperator.UntilDate, DateValue = "2035-01-01", Rate = 0.06m }],
            unitValue: 1m
        )
        { Id = 3 };

        // Carry-in entry for Instrument 1 (purchased 6 months before start)
        var carriedEntry1 = new BondAccountEntry(1, 1, new DateTime(2019, 7, 1, 0, 0, 0, DateTimeKind.Utc), 1000m, 1000m, 1);

        // Account entries added via Add() to ensure cumulative values are computed accurately
        var entry2 = new BondAccountEntry(1, 2, startDateTime, 0, 2000m, 2);
        var entry3 = new BondAccountEntry(1, 3, new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc), 0, 1500m, 3);

        var account = new BondAccount(
            1,
            1,
            "10-Year Long-Horizon Account",
            entries: null,
            accountType: AccountLabel.Other,
            nextOlderEntries: new Dictionary<int, BondAccountEntry> { [1] = carriedEntry1 }
        );
        account.Add(entry2);
        account.Add(entry3);

        List<BondDetails> detailsList = [bondDetails1, bondDetails2, bondDetails3];

        // Act: Measure execution time and heap allocation for 10-year daily pricing
        var startAllocated = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        var dailyPrices = account.GetDailyPrice(startDate, endDate, detailsList);
        stopwatch.Stop();
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - startAllocated;

        var totalDays = dailyPrices.Count;
        var throughputDaysPerMs = stopwatch.ElapsedMilliseconds > 0
            ? (double)totalDays / stopwatch.ElapsedMilliseconds
            : totalDays;

        output.WriteLine($"[10-Year History] Elapsed: {stopwatch.ElapsedMilliseconds} ms, Allocated: {allocatedBytes:N0} bytes, Days: {totalDays}, Throughput: {throughputDaysPerMs:F2} days/ms");

        // Assert: Explicit budgets catch accidental regressions while leaving headroom for CI variance.
        Assert.InRange(stopwatch.ElapsedMilliseconds, 0, 5_000);
        Assert.InRange(allocatedBytes, 0, 30_000_000);

        var expectedDays = endDate.DayNumber - startDate.DayNumber + 1;
        Assert.Equal(expectedDays, totalDays);

        // Start day includes carried-over accrued value from Instrument 1 + Instrument 2 initial capital
        var initialCarriedPrice = carriedEntry1.GetPriceAt(startDate, bondDetails1);
        Assert.True(initialCarriedPrice > 1000m, "Carried entry should have accrued interest from 2019-07-01 to 2020-01-01.");
        Assert.Equal(initialCarriedPrice + 2000m, dailyPrices[startDate]);

        // Verify equivalence at each year boundary across the entire 10-year span
        for (int year = startDate.Year; year <= endDate.Year; year++)
        {
            var date = new DateOnly(year, 1, 1);
            var dt = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            decimal expectedSum = 0;
            var e1 = account.GetThisOrNextOlder(dt, 1);
            if (e1 is not null) expectedSum += e1.GetPriceAt(date, bondDetails1);

            var e2 = account.GetThisOrNextOlder(dt, 2);
            if (e2 is not null) expectedSum += e2.GetPriceAt(date, bondDetails2);

            var e3 = account.GetThisOrNextOlder(dt, 3);
            if (e3 is not null) expectedSum += e3.GetPriceAt(date, bondDetails3);

            Assert.Equal(expectedSum, dailyPrices[date]);
        }

        // Final day verification
        var finalDt = endDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        decimal finalExpected =
            account.GetThisOrNextOlder(finalDt, 1)!.GetPriceAt(endDate, bondDetails1) +
            account.GetThisOrNextOlder(finalDt, 2)!.GetPriceAt(endDate, bondDetails2) +
            account.GetThisOrNextOlder(finalDt, 3)!.GetPriceAt(endDate, bondDetails3);

        Assert.Equal(finalExpected, dailyPrices[endDate]);
    }

    [Fact]
    public void GetDailyPrice_OpeningBoundary_NextOlderEntriesCarriedIn_EvaluatesAccrualFromPostingDate()
    {
        // Arrange: Valuation begins 2024-06-01, but the bond entry was posted on 2024-01-01
        var postingDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var startDate = new DateOnly(2024, 6, 1);
        var endDate = new DateOnly(2024, 6, 10);

        var calculationMethod = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2025-01-01",
            Rate = 0.0365m
        };

        var bondDetails = new BondDetails(
            "Carried Bond",
            "Issuer",
            DateOnly.FromDateTime(postingDate),
            endDate.AddYears(1),
            [calculationMethod],
            unitValue: 1m
        )
        { Id = 1 };

        var carriedEntry = new BondAccountEntry(1, 1, postingDate, 1000m, 1000m, 1);

        // Account has no current Entries in range, only NextOlderEntries
        var account = new BondAccount(
            1,
            1,
            "Carry Account",
            [],
            AccountLabel.Other,
            nextOlderEntries: new Dictionary<int, BondAccountEntry> { [1] = carriedEntry }
        );

        // Act
        var result = account.GetDailyPrice(startDate, endDate, [bondDetails]);

        // Assert: Prices should reflect ~5 months of accrual starting from 2024-01-01
        Assert.Equal(10, result.Count);
        var startPrice = result[startDate];
        var expectedStartPrice = carriedEntry.GetPriceAt(startDate, bondDetails);

        Assert.Equal(expectedStartPrice, startPrice);
        Assert.True(startPrice > 1000m, "Opening boundary must reflect accrual from entry posting date.");
    }

    [Fact]
    public void GetDailyPrice_IntradayMultipleEntries_PreservesLatestEntryPrecedence()
    {
        // Arrange: Multiple entries on the same calendar date at different times
        var baseDate = new DateOnly(2023, 5, 1);
        var morning = new DateTime(2023, 5, 1, 9, 0, 0, DateTimeKind.Utc);
        var afternoon = new DateTime(2023, 5, 1, 15, 0, 0, DateTimeKind.Utc);

        var morningEntry = new BondAccountEntry(1, 1, morning, 1000m, 1000m, 1);
        var afternoonEntry = new BondAccountEntry(1, 2, afternoon, 1500m, 500m, 1);

        var account = new BondAccount(1, 1, "Intraday Account", AccountLabel.Other);
        account.Add(morningEntry);
        account.Add(afternoonEntry);

        var calculationMethod = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2024-01-01",
            Rate = 0.0365m
        };

        var bondDetails = new BondDetails(
            "Intraday Bond",
            "Issuer",
            baseDate,
            baseDate.AddYears(1),
            [calculationMethod],
            unitValue: 1m
        )
        { Id = 1 };

        // Act
        var result = account.GetDailyPrice(baseDate, baseDate.AddDays(2), [bondDetails]);

        // Assert: The entry with the latest PostingDate on that day (afternoonEntry with Value=1500) takes precedence
        Assert.Equal(3, result.Count);
        Assert.Equal(1500m, result[baseDate]);
    }

    [Fact]
    public void GetDailyPrice_NetZeroBalance_OmitsZeroEntriesFromDailyPrice()
    {
        // Arrange: A buy followed by a complete sell on day 3
        var startDate = new DateOnly(2023, 1, 1);
        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var sellDateTime = startDate.AddDays(2).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var buyEntry = new BondAccountEntry(1, 1, startDateTime, 0, 1000m, 1);
        var sellEntry = new BondAccountEntry(1, 2, sellDateTime, 0, -1000m, 1);

        var account = new BondAccount(1, 1, "Zero Holdings Account", AccountLabel.Other);
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
            startDate,
            startDate.AddYears(1),
            [calculationMethod],
            unitValue: 1m
        )
        { Id = 1 };

        // Act: Query spanning 5 days (days 0, 1 have positive balance; days 2, 3, 4 have 0 balance)
        var result = account.GetDailyPrice(startDate, startDate.AddDays(4), [bondDetails]);

        // Assert: Only non-zero total valuation days should be present in the result
        Assert.Equal(2, result.Count);
        Assert.Contains(startDate, result.Keys);
        Assert.Contains(startDate.AddDays(1), result.Keys);
        Assert.DoesNotContain(startDate.AddDays(2), result.Keys);
        Assert.DoesNotContain(startDate.AddDays(3), result.Keys);
        Assert.DoesNotContain(startDate.AddDays(4), result.Keys);
    }

    [Fact]
    public void GetDailyPrice_RateChangeAcrossBoundary_CalculatesAccrualSmoothly()
    {
        // Arrange: Bond rate changes from 3.65% to 7.30% halfway through
        var startDate = new DateOnly(2023, 1, 1);
        var splitDate = new DateOnly(2023, 7, 1);
        var endDate = new DateOnly(2023, 12, 31);
        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var method1 = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = DateOperator.UntilDate,
            DateValue = splitDate.ToString("yyyy-MM-dd"),
            Rate = 0.0365m
        };

        var method2 = new BondCalculationMethod
        {
            Id = 2,
            DateOperator = DateOperator.UntilDate,
            DateValue = endDate.AddYears(1).ToString("yyyy-MM-dd"),
            Rate = 0.0730m
        };

        var bondDetails = new BondDetails(
            "Step-Up Bond",
            "Issuer",
            startDate,
            endDate.AddYears(1),
            [method1, method2],
            unitValue: 1m
        )
        { Id = 1 };

        var entry = new BondAccountEntry(1, 1, startDateTime, 0, 1000m, 1);
        var account = new BondAccount(1, 1, "Rate Change Account", AccountLabel.Other);
        account.Add(entry);

        // Act
        var result = account.GetDailyPrice(startDate, endDate, [bondDetails]);

        // Assert
        Assert.Equal(endDate.DayNumber - startDate.DayNumber + 1, result.Count);

        var dayBeforeSplit = splitDate.AddDays(-1);
        var dayOfSplit = splitDate;
        var dayAfterSplit = splitDate.AddDays(1);

        // Continuous growth across the boundary
        Assert.True(result[dayOfSplit] > result[dayBeforeSplit]);
        Assert.True(result[dayAfterSplit] > result[dayOfSplit]);

        // Daily accrual after split should be higher due to double interest rate
        var dailyGrowthBefore = result[dayBeforeSplit] - result[dayBeforeSplit.AddDays(-1)];
        var dailyGrowthAfter = result[dayAfterSplit] - result[dayOfSplit];
        Assert.True(dailyGrowthAfter > dailyGrowthBefore);
    }

    [Fact]
    public void GetDailyPrice_CapitalizationAtExactly365Days_CompoundsCapitalAcrossLeapYears()
    {
        // Arrange: Start on 2024-01-01 (2024 is a leap year with 366 days)
        var startDate = new DateOnly(2024, 1, 1);
        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var day364 = startDate.AddDays(364); // 2024-12-30
        var day365 = startDate.AddDays(365); // 2024-12-31 (exactly 365 days)
        var day366 = startDate.AddDays(366); // 2025-01-01 (leap year 1-year calendar anniversary)

        var calculationMethod = new BondCalculationMethod
        {
            Id = 1,
            DateOperator = DateOperator.UntilDate,
            DateValue = "2026-01-01",
            Rate = 0.05m
        };

        var bondDetails = new BondDetails(
            "Leap Year Bond",
            "Issuer",
            startDate,
            startDate.AddYears(2),
            [calculationMethod],
            unitValue: 1m
        )
        { Id = 1 };

        var entry = new BondAccountEntry(1, 1, startDateTime, 0, 1000m, 1);
        var account = new BondAccount(1, 1, "Capitalization Test Account", AccountLabel.Other);
        account.Add(entry);

        // Act
        var result = account.GetDailyPrice(startDate, day366, [bondDetails]);

        // Assert: 365-day capitalization rule compounds capital on day 365 (2024-12-31)
        Assert.Equal(367, result.Count);

        var growthDay365 = result[day365] - result[day364];
        var growthDay366 = result[day366] - result[day365];

        // On day 366, daily interest accrues on the capitalized amount (current > original capital),
        // so daily growth on day 366 must be strictly greater than or equal to day 365
        Assert.True(growthDay366 >= growthDay365);
        Assert.Equal(entry.GetPriceAt(day366, bondDetails), result[day366]);
    }

    [Fact]
    public void GetDailyPrice_StartGreaterThanEnd_ReturnsEmptyDictionary()
    {
        // Arrange
        var startDate = new DateOnly(2024, 6, 1);
        var endDate = new DateOnly(2024, 5, 1);

        var bondDetails = new BondDetails(
            "Bond",
            "Issuer",
            new DateOnly(2024, 1, 1),
            new DateOnly(2025, 1, 1),
            [new BondCalculationMethod { Id = 1, DateOperator = DateOperator.UntilDate, DateValue = "2025-01-01", Rate = 0.04m }],
            unitValue: 1m
        )
        { Id = 1 };

        var account = new BondAccount(1, 1, "Account", AccountLabel.Other);
        account.Add(new BondAccountEntry(1, 1, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), 0, 1000m, 1));

        // Act
        var result = account.GetDailyPrice(startDate, endDate, [bondDetails]);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetDailyPrice_MissingBondDetails_ThrowsInvalidOperationException()
    {
        // Arrange: Account has entries for bond ID 1 and bond ID 2, but details only provided for bond ID 1
        var startDate = new DateOnly(2024, 1, 1);
        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var entry1 = new BondAccountEntry(1, 1, startDateTime, 0, 1000m, 1);
        var entry2 = new BondAccountEntry(1, 2, startDateTime, 0, 1000m, 2);

        var account = new BondAccount(1, 1, "Missing Details Account", AccountLabel.Other);
        account.Add(entry1);
        account.Add(entry2);

        var bondDetails1 = new BondDetails(
            "Bond 1",
            "Issuer",
            startDate,
            startDate.AddYears(1),
            [new BondCalculationMethod { Id = 1, DateOperator = DateOperator.UntilDate, DateValue = "2025-01-01", Rate = 0.04m }],
            unitValue: 1m
        )
        { Id = 1 };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            account.GetDailyPrice(startDate, startDate.AddDays(10), [bondDetails1]));

        Assert.Contains("Bond valuation requires details for bond ids: 2", exception.Message);
    }
}