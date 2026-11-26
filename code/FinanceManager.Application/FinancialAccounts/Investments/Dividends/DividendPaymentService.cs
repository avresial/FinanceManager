using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;

namespace FinanceManager.Application.FinancialAccounts.Investments.Dividends;

public sealed class DividendPaymentService(IDividendPaymentRepository payments,
    ICurrencyRepository currencies)
{
    public async Task<DividendPaymentPage> GetHistory(int userId, int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 50 || page > int.MaxValue / pageSize)
            throw new ArgumentException("Page must be positive and page size must be between 1 and 50.");
        return await payments.GetHistory(userId, page, pageSize, ct);
    }

    private static void RequirePayment(DividendPayment payment, int userId)
    {
        if (payment.UserId != userId) throw new KeyNotFoundException("Dividend payment not found.");
    }

    public async Task<IReadOnlyList<DividendHoldingOption>> GetHoldings(int userId, CancellationToken ct = default)
    {
        return await payments.GetHoldings(userId, ct);
    }

    public async Task<long> Save(int userId, long? id, DividendPaymentRequest request, CancellationToken ct = default)
    {
        var payment = id.HasValue ? await payments.Get(id.Value, ct) ?? throw new KeyNotFoundException("Dividend payment not found.") : null;
        if (payment is not null) RequirePayment(payment, userId);
        if (request.PaymentDate == default || request.PaymentDate > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentException("Choose a historical payment date, up to today.");
        if (request.Amount <= 0 || request.Amount >= 10000000000m || decimal.Round(request.Amount, 8) != request.Amount)
            throw new ArgumentException("Cash received must be positive, below 10 billion, and have at most eight decimal places.");
        var currency = request.Currency?.Trim().ToUpperInvariant();
        if (currency?.Length != 3 || await currencies.GetByCode(currency, ct) is null)
            throw new ArgumentException("Choose a supported payout currency.");
        if (request.Notes?.Length > 512) throw new ArgumentException("Notes must be 512 characters or fewer.");
        // Historical records remain editable after their original trades have been removed.
        if (request.AssetListingId <= 0 || (payment?.AssetListingId != request.AssetListingId
            && !await payments.HasHolding(userId, request.AssetListingId, ct)))
            throw new ArgumentException("Choose a holding associated with your investments.");
        var now = DateTimeOffset.UtcNow;
        var isNew = payment is null;
        payment ??= new DividendPayment { UserId = userId, CreatedAt = now };
        payment.AssetListingId = request.AssetListingId;
        payment.PaymentDate = request.PaymentDate;
        payment.Amount = request.Amount;
        payment.Currency = currency;
        payment.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        payment.UpdatedAt = now;
        if (isNew) await payments.Add(payment, ct);
        else await payments.Save(ct);
        return payment.Id;
    }

    public async Task Delete(int userId, long id, CancellationToken ct = default)
    {
        var payment = await payments.Get(id, ct) ?? throw new KeyNotFoundException("Dividend payment not found.");
        RequirePayment(payment, userId);
        await payments.Delete(payment, ct);
    }
}