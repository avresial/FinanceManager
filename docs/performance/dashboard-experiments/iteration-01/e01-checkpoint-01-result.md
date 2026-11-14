# E01 checkpoint 1 result

Added two parity tests to BondDashboardContextTests.cs (theory over ascending/descending/shuffled date orders; gap/expiry fact) plus a CreateParityDetails helper, reusing the existing mock repository/setup patterns. Semantic guards only - no performance or complexity claims.

## Test run

Command (from code/): `dotnet test --project ./FinanceManager.Tests.Unit/FinanceManager.Tests.Unit.csproj`
Log: docs/performance/dashboard-experiments/iteration-01/raw/e01-checkpoint-01-tests.log

Exit code and summary from log:

```
  failed: 43
  succeeded: 2206
  skipped: 0
  duration: 23s 170ms
Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exitcodes)
exit code: 2
```

## Diff (BondDashboardContextTests.cs)

```diff
diff --git a/code/FinanceManager.Tests.Unit/Application/FinancialAccounts/Bond/Valuation/BondDashboardContextTests.cs b/code/FinanceManager.Tests.Unit/Application/FinancialAccounts/Bond/Valuation/BondDashboardContextTests.cs
index 8a080b92..95648ef6 100644
--- a/code/FinanceManager.Tests.Unit/Application/FinancialAccounts/Bond/Valuation/BondDashboardContextTests.cs
+++ b/code/FinanceManager.Tests.Unit/Application/FinancialAccounts/Bond/Valuation/BondDashboardContextTests.cs
@@ -157,6 +157,73 @@ public class BondDashboardContextTests
         Assert.Equal("Bond valuation requires details for bond id 42.", exception.Message);
     }
 
+    [Theory]
+    [MemberData(nameof(ParityDateOrders))]
+    public async Task GetOrComputePrice_MatchesEntryGetPriceAt_ForAscendingDescendingAndShuffledDates(int[] dayOffsets)
+    {
+        var details = CreateParityDetails(1);
+        var repository = new Mock<IBondDetailsRepository>();
+        repository
+            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
+            .ReturnsAsync((IReadOnlyList<BondDetails>)[details]);
+
+        var context = new BondDashboardContext(repository.Object);
+        var account = new BondAccount(
+            1,
+            10,
+            "Bonds",
+            [new BondAccountEntry(10, 1, new DateTime(2023, 1, 1), 10m, 10m, 1)],
+            AccountLabel.Other);
+        await context.LoadReferencedDetailsAsync([account], TestContext.Current.CancellationToken);
+
+        var entry = account.Entries[0];
+        var baseDate = new DateOnly(2023, 1, 1);
+        foreach (var pass in new[] { dayOffsets, dayOffsets })
+        {
+            foreach (var dayOffset in pass)
+            {
+                var date = baseDate.AddDays(dayOffset);
+                Assert.Equal(entry.GetPriceAt(date, details), context.GetOrComputePrice(account.AccountId, entry, date));
+            }
+        }
+    }
+
+    [Fact]
+    public async Task GetOrComputePrice_MatchesEntryGetPriceAt_AroundMethodExpiryGap()
+    {
+        var details = CreateParityDetails(1, untilDate: "2023-01-05");
+        var repository = new Mock<IBondDetailsRepository>();
+        repository
+            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
+            .ReturnsAsync((IReadOnlyList<BondDetails>)[details]);
+
+        var context = new BondDashboardContext(repository.Object);
+        var account = new BondAccount(
+            1,
+            10,
+            "Bonds",
+            [new BondAccountEntry(10, 1, new DateTime(2023, 1, 1), 10m, 10m, 1)],
+            AccountLabel.Other);
+        await context.LoadReferencedDetailsAsync([account], TestContext.Current.CancellationToken);
+
+        var entry = account.Entries[0];
+        var baseDate = new DateOnly(2023, 1, 1);
+        foreach (var dayOffset in new[] { 0, 4, 5, 6, 10 })
+        {
+            var date = baseDate.AddDays(dayOffset);
+            Assert.Equal(entry.GetPriceAt(date, details), context.GetOrComputePrice(account.AccountId, entry, date));
+        }
+    }
+
+    public static IEnumerable<object[]> ParityDateOrders()
+    {
+        var ascending = new[] { -1, 0, 1, 364, 365, 366, 730 };
+        var shuffled = new[] { 365, -1, 730, 1, 364, 366, 0 };
+        yield return [ascending];
+        yield return [ascending.Reverse().ToArray()];
+        yield return [shuffled];
+    }
+
     private static BondDetails CreateDetails(int id, decimal unitValue = 100m) =>
         new(
             "Bond",
@@ -175,4 +242,23 @@ public class BondDashboardContextTests
         {
             Id = id,
         };
+
+    private static BondDetails CreateParityDetails(int id, string untilDate = "2027-01-01") =>
+        new(
+            "Bond",
+            "Issuer",
+            new DateOnly(2023, 1, 1),
+            new DateOnly(2027, 1, 1),
+            [new BondCalculationMethod
+            {
+                DateOperator = DateOperator.UntilDate,
+                DateValue = untilDate,
+                Rate = 0.0365m,
+            }],
+            DefaultCurrency.PLN,
+            BondType.InflationBond,
+            100m)
+        {
+            Id = id,
+        };
 }
\ No newline at end of file
```


## Parent verification

The full unit suite passed with `LANG=en_US.UTF-8 LC_ALL=en_US.UTF-8`: 2249 passed, zero failed, exit 0. See `raw/e01-parent-unit-tests.log`. The original failure log is retained. No production correction was required.
