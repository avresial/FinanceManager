using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

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

    private static string Normalize(string currency) => currency.Trim().ToUpperInvariant();

    // Rates are daily; store the UTC day start so lookups are exact and PostgreSQL accepts the value.
    private static DateTime NormalizeDate(DateTime date) => DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
}