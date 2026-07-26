using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Repositories;

public class ExchangeRateRepository(AppDbContext context) : IExchangeRateRepository
{
    // Exchange-rate lookups are fanned out in parallel batches (one task per date), but they all
    // share this scoped repository and its DbContext, which is not thread-safe. Serialise the
    // context access here so callers may keep their parallelism for the provider HTTP calls.
    private readonly SemaphoreSlim _contextLock = new(1, 1);

    public async Task<decimal?> Get(string fromCurrency, string toCurrency, DateTime date, CancellationToken ct = default)
    {
        var from = Normalize(fromCurrency);
        var to = Normalize(toCurrency);
        var day = NormalizeDate(date);

        await _contextLock.WaitAsync(ct);
        try
        {
            var rate = await context.ExchangeRates
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.FromCurrency == from && x.ToCurrency == to && x.Date == day, ct);

            return rate?.Rate;
        }
        finally
        {
            _contextLock.Release();
        }
    }

    public async Task Add(string fromCurrency, string toCurrency, DateTime date, decimal rate, CancellationToken ct = default)
    {
        var from = Normalize(fromCurrency);
        var to = Normalize(toCurrency);
        var day = NormalizeDate(date);

        await _contextLock.WaitAsync(ct);
        try
        {
            var existing = await context.ExchangeRates
                .FirstOrDefaultAsync(x => x.FromCurrency == from && x.ToCurrency == to && x.Date == day, ct);

            if (existing is not null)
            {
                existing.Rate = rate;
            }
            else
            {
                context.ExchangeRates.Add(new ExchangeRate
                {
                    FromCurrency = from,
                    ToCurrency = to,
                    Date = day,
                    Rate = rate
                });
            }

            try
            {
                await context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // The unique (from, to, date) index rejected the insert. If the row now exists, a
                // concurrent request stored the rate between the lookup above and this insert and
                // nothing is lost; any other write failure must surface unchanged.
                context.ChangeTracker.Clear();
                if (!await context.ExchangeRates.AsNoTracking()
                        .AnyAsync(x => x.FromCurrency == from && x.ToCurrency == to && x.Date == day, ct))
                    throw;
            }
        }
        finally
        {
            _contextLock.Release();
        }
    }

    public async Task<IReadOnlyDictionary<(string From, string To, DateTime Date), decimal>> GetRange(string fromCurrency, string toCurrency, DateTime dateStart, DateTime dateEnd, CancellationToken ct = default)
    {
        var from = Normalize(fromCurrency);
        var to = Normalize(toCurrency);
        var startDay = NormalizeDate(dateStart);
        var endDay = NormalizeDate(dateEnd);

        if (startDay > endDay)
            (startDay, endDay) = (endDay, startDay);

        await _contextLock.WaitAsync(ct);
        try
        {
            var directRates = await context.ExchangeRates
                .AsNoTracking()
                .Where(x => x.FromCurrency == from && x.ToCurrency == to && x.Date >= startDay && x.Date <= endDay)
                .ToDictionaryAsync(x => (x.FromCurrency, x.ToCurrency, x.Date), x => x.Rate, ct);

            var inverseRates = await context.ExchangeRates
                .AsNoTracking()
                .Where(x => x.FromCurrency == to && x.ToCurrency == from && x.Date >= startDay && x.Date <= endDay)
                .ToListAsync(ct);

            var result = new Dictionary<(string From, string To, DateTime Date), decimal>(directRates);
            foreach (var inverse in inverseRates)
            {
                var key = (from, to, inverse.Date);
                if (!result.ContainsKey(key) && inverse.Rate != 0)
                    result[key] = 1m / inverse.Rate;
            }

            return result;
        }
        finally
        {
            _contextLock.Release();
        }
    }

    public async Task<DateTime?> GetLatestDate(string fromCurrency, string toCurrency, CancellationToken ct = default)
    {
        var from = Normalize(fromCurrency);
        var to = Normalize(toCurrency);

        await _contextLock.WaitAsync(ct);
        try
        {
            var dates = context.ExchangeRates
                .AsNoTracking()
                .Where(x => x.FromCurrency == from && x.ToCurrency == to)
                .Select(x => x.Date);

            // MaxAsync throws on an empty sequence; project to nullable and use a scalar aggregate so
            // an unseen pair returns null instead of blowing up.
            return await dates.MaxAsync(d => (DateTime?)d, ct);
        }
        finally
        {
            _contextLock.Release();
        }
    }

    public async Task<IReadOnlyCollection<DateTime>> GetExistingDates(string fromCurrency, string toCurrency, DateTime dateStart, DateTime dateEnd, CancellationToken ct = default)
    {
        var from = Normalize(fromCurrency);
        var to = Normalize(toCurrency);
        var startDay = NormalizeDate(dateStart);
        var endDay = NormalizeDate(dateEnd);

        if (startDay > endDay)
            (startDay, endDay) = (endDay, startDay);

        await _contextLock.WaitAsync(ct);
        try
        {
            return await context.ExchangeRates
                .AsNoTracking()
                .Where(x => x.FromCurrency == from && x.ToCurrency == to && x.Date >= startDay && x.Date <= endDay)
                .Select(x => x.Date)
                .ToListAsync(ct);
        }
        finally
        {
            _contextLock.Release();
        }
    }

    public async Task<int> AddRange(string fromCurrency, string toCurrency, IReadOnlyCollection<(DateTime Date, decimal Rate)> rates, CancellationToken ct = default)
    {
        if (rates.Count == 0) return 0;

        var from = Normalize(fromCurrency);
        var to = Normalize(toCurrency);

        // Collapse duplicate dates in the incoming batch (last wins) and normalise the kind so the
        // in-memory existence check matches what the database stores.
        var byDate = new Dictionary<DateTime, decimal>();
        foreach (var (date, rate) in rates)
            byDate[NormalizeDate(date)] = rate;

        await _contextLock.WaitAsync(ct);
        try
        {
            var minDay = byDate.Keys.Min();
            var maxDay = byDate.Keys.Max();

            var existing = await context.ExchangeRates
                .AsNoTracking()
                .Where(x => x.FromCurrency == from && x.ToCurrency == to && x.Date >= minDay && x.Date <= maxDay)
                .Select(x => x.Date)
                .ToListAsync(ct);

            var existingDates = existing.ToHashSet();

            var toInsert = byDate
                .Where(kvp => !existingDates.Contains(kvp.Key))
                .Select(kvp => new ExchangeRate
                {
                    FromCurrency = from,
                    ToCurrency = to,
                    Date = kvp.Key,
                    Rate = kvp.Value
                })
                .ToList();

            if (toInsert.Count == 0) return 0;

            context.ExchangeRates.AddRange(toInsert);
            try
            {
                await context.SaveChangesAsync(ct);
                return toInsert.Count;
            }
            catch (DbUpdateException)
            {
                // A concurrent backfill inserted overlapping dates between the read above and this
                // save. Drop this context's pending inserts, then re-insert only the dates that are
                // still missing so completed rows stay committed and the run remains idempotent.
                context.ChangeTracker.Clear();

                var nowExisting = (await context.ExchangeRates
                        .AsNoTracking()
                        .Where(x => x.FromCurrency == from && x.ToCurrency == to && x.Date >= minDay && x.Date <= maxDay)
                        .Select(x => x.Date)
                        .ToListAsync(ct))
                    .ToHashSet();

                var retry = toInsert.Where(r => !nowExisting.Contains(r.Date)).ToList();
                if (retry.Count == 0) return 0;

                context.ExchangeRates.AddRange(retry);
                await context.SaveChangesAsync(ct);
                return retry.Count;
            }
        }
        finally
        {
            _contextLock.Release();
        }
    }

    private static string Normalize(string currency) => currency.Trim().ToUpperInvariant();

    // Rates are daily; store the UTC day start so lookups are exact and PostgreSQL accepts the value.
    private static DateTime NormalizeDate(DateTime date) => DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
}