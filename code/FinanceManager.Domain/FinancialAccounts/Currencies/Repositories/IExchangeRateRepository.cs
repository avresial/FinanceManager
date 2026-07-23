namespace FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;

public interface IExchangeRateRepository
{
    public Task<decimal?> Get(string fromCurrency, string toCurrency, DateTime date, CancellationToken ct = default);
    public Task Add(string fromCurrency, string toCurrency, DateTime date, decimal rate, CancellationToken ct = default);
    public Task<IReadOnlyDictionary<(string From, string To, DateTime Date), decimal>> GetRange(string fromCurrency, string toCurrency, DateTime dateStart, DateTime dateEnd, CancellationToken ct = default);

    /// <summary>
    /// The most recent stored date for a direct (from → to) pair, or <c>null</c> when none exists.
    /// Lets a backfill decide whether a pair is fresh in a single query instead of scanning a range.
    /// </summary>
    public Task<DateTime?> GetLatestDate(string fromCurrency, string toCurrency, CancellationToken ct = default);

    /// <summary>
    /// The distinct stored dates for a direct (from → to) pair within [start, end]. Used to filter a
    /// provider's returned points down to the ones missing from the database before a bulk insert.
    /// </summary>
    public Task<IReadOnlyCollection<DateTime>> GetExistingDates(string fromCurrency, string toCurrency, DateTime dateStart, DateTime dateEnd, CancellationToken ct = default);

    /// <summary>
    /// Inserts many daily rates for a direct (from → to) pair in one transaction, skipping any date
    /// already present. Returns the number of rows actually inserted. Safe to run concurrently: the
    /// unique (from, to, date) index is the final guard and a conflicting insert is treated as a
    /// no-op rather than an error.
    /// </summary>
    public Task<int> AddRange(string fromCurrency, string toCurrency, IReadOnlyCollection<(DateTime Date, decimal Rate)> rates, CancellationToken ct = default);
}