using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;

namespace FinanceManager.Application.FinancialAccounts.Investments.Dividends;

public sealed class DividendPaymentService(IDividendPaymentRepository payments,
    IAccountRepository<InvestmentAccount> accounts, ICurrencyRepository currencies)
{
    private async Task RequireOwner(int userId, int accountId)
    {
        var account = await accounts.Get(accountId) ?? throw new KeyNotFoundException("Investment account not found.");
        if (account.UserId != userId) throw new UnauthorizedAccessException();
    }

    public async Task<DividendPaymentPage> GetHistory(int userId, int accountId, int page, int pageSize, CancellationToken ct = default)
    {
        await RequireOwner(userId, accountId);
        if (page < 1 || pageSize is < 1 or > 50 || page > int.MaxValue / pageSize)
            throw new ArgumentException("Page must be positive and page size must be between 1 and 50.");
        return await payments.GetHistory(userId, accountId, page, pageSize, ct);
    }

    public async Task<IReadOnlyList<DividendHoldingOption>> GetHoldings(int userId, int accountId, CancellationToken ct = default)
    {
        await RequireOwner(userId, accountId);
        return await payments.GetHoldings(userId, accountId, ct);
    }

    public async Task<long> Save(int userId, int accountId, long? id, DividendPaymentRequest request, CancellationToken ct = default)
    {
        await RequireOwner(userId, accountId);
        var payment = id.HasValue ? await payments.Get(id.Value, ct) ?? throw new KeyNotFoundException("Dividend payment not found.") : null;
        if (payment is not null && (payment.UserId != userId || payment.AccountId != accountId)) throw new UnauthorizedAccessException();
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
            && !await payments.HasHolding(userId, accountId, request.AssetListingId, ct)))
            throw new ArgumentException("Choose a holding associated with this investment account.");
        var now = DateTimeOffset.UtcNow;
        var isNew = payment is null;
        payment ??= new DividendPayment { UserId = userId, AccountId = accountId, CreatedAt = now };
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

    public async Task Delete(int userId, int accountId, long id, CancellationToken ct = default)
    {
        await RequireOwner(userId, accountId);
        var payment = await payments.Get(id, ct) ?? throw new KeyNotFoundException("Dividend payment not found.");
        if (payment.UserId != userId || payment.AccountId != accountId) throw new UnauthorizedAccessException();
        await payments.Delete(payment, ct);
    }
}