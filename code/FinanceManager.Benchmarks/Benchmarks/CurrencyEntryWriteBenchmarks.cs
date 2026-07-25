using BenchmarkDotNet.Attributes;
using FinanceManager.Domain.FinancialAccounts.Currencies.Commands;

namespace FinanceManager.Benchmarks.Benchmarks;

/// <summary>
/// The write path a user hits when recording a transaction by hand, plus the balance recalculation that
/// follows a correction.
/// </summary>
/// <remarks>
/// <para>
/// Adding an entry is not just an insert: it invalidates the user's cached read models and queues the
/// entry for label classification. That invalidation is why the write path belongs in a UX baseline —
/// a slow write is felt directly, and an over-broad invalidation makes the *next* read slow too.
/// </para>
/// <para>
/// Entries are appended at a fixed date just past the end of the seeded history, so each insert lands at
/// the head of the account and the balance recalculation has nothing after it to rewrite. Iteration
/// cleanup deletes them again, which keeps the dataset stable across iterations and leaves the database
/// as the read benchmarks found it. Within a single iteration the appended rows do accumulate — the
/// account grows by roughly a hundred entries on a base of well over a thousand — so read a few percent
/// of drift here as noise, not signal.
/// </para>
/// </remarks>
[BenchmarkCategory("Writes")]
public class CurrencyEntryWriteBenchmarks : ApiBenchmark
{
    private const decimal _entryValue = 4_200m;
    private const decimal _entryValueChange = -25m;

    private int _accountId;
    private DateTime _appendDate;

    protected override void Configure()
    {
        _accountId = Scenario.PrimaryCurrencyAccountId;
        _appendDate = Scenario.End.AddDays(1);
    }

    protected override async Task Prime()
    {
        await AddEntry();
        await RecalculateBalance();
        Cleanup();
    }

    [IterationCleanup]
    public void Cleanup() => Api.ResetAppendedEntries(_accountId, Scenario.End);

    [Benchmark(Description = "POST api/CurrencyEntry (append entry)", Baseline = true)]
    public Task<long> AddEntry() =>
        Post("api/CurrencyEntry", new AddCurrencyAccountEntry(
            AccountId: _accountId,
            // The command validates EntryId as >= 1, but the controller ignores it and lets the
            // repository assign the real id, so any valid placeholder works here.
            EntryId: 1,
            PostingDate: _appendDate,
            Value: _entryValue,
            ValueChange: _entryValueChange,
            Description: "Benchmark append",
            ContractorDetails: null));

    /// <summary>
    /// Rewrites every running balance on the account. Idempotent — recalculating already-correct values
    /// produces the same rows — so it can be measured repeatedly without drifting the dataset, while
    /// still exercising the heaviest write in the app.
    /// </summary>
    [Benchmark(Description = "POST api/CurrencyEntry/Recalculate/{accountId}")]
    public Task<long> RecalculateBalance() => Post($"api/CurrencyEntry/Recalculate/{_accountId}", new { });
}