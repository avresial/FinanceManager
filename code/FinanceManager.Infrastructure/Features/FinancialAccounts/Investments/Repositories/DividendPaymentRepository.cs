using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Investments.Repositories;

public class DividendPaymentRepository(AppDbContext context) : IDividendPaymentRepository
{
    public Task<DividendPayment?> Get(long id, CancellationToken ct = default) =>
        context.DividendPayments.FirstOrDefaultAsync(x => x.Id == id, ct);

    private IQueryable<DividendPayment> Payments(int userId, int? accountId) =>
        accountId is int id
            ? context.DividendPayments.Where(x => x.UserId == userId && x.AccountId == id)
            : context.DividendPayments.Where(x => x.UserId == userId);

    private IQueryable<InvestmentTransaction> Trades(int userId, int? accountId) =>
        accountId is int id
            ? context.InvestmentTransactions.Where(x => x.UserId == userId && x.AccountId == id)
            : context.InvestmentTransactions.Where(x => x.UserId == userId);

    public async Task<DividendPaymentPage> GetHistory(int userId, int? accountId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = Payments(userId, accountId).AsNoTracking();
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.PaymentDate).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new DividendPaymentDto(x.Id, x.AccountId, x.AssetListingId, x.AssetListing.Asset.Name,
                x.AssetListing.Ticker, x.PaymentDate, x.Amount, x.Currency, x.Notes)).ToListAsync(ct);
        return new(items, count, page, pageSize);
    }

    public async Task<IReadOnlyList<DividendHoldingOption>> GetHoldings(int userId, int? accountId, CancellationToken ct = default) =>
        await context.AssetListings.AsNoTracking()
            .Where(x => Trades(userId, accountId).Any(t => t.AssetListingId == x.Id)
                || Payments(userId, accountId).Any(p => p.AssetListingId == x.Id))
            .OrderBy(x => x.Asset.Name).ThenBy(x => x.Id)
            .Select(x => new DividendHoldingOption(x.Id, x.Asset.Name, x.Ticker, x.ExchangeMic, x.TradingCurrency)).ToListAsync(ct);

    public async Task<bool> HasHolding(int userId, int? accountId, long listingId, CancellationToken ct = default) =>
        await Trades(userId, accountId).AnyAsync(x => x.AssetListingId == listingId, ct)
        || await Payments(userId, accountId).AnyAsync(x => x.AssetListingId == listingId, ct);

    public async Task<DividendPayment> Add(DividendPayment payment, CancellationToken ct = default)
    {
        context.DividendPayments.Add(payment);
        await context.SaveChangesAsync(ct);
        return payment;
    }
    public async Task Save(CancellationToken ct = default) => await context.SaveChangesAsync(ct);
    public async Task Delete(DividendPayment payment, CancellationToken ct = default)
    {
        context.DividendPayments.Remove(payment);
        await context.SaveChangesAsync(ct);
    }
}