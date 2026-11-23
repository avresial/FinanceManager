using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Repositories;

public interface IInvestmentTransactionRepository
{
    Task<IReadOnlyList<InvestmentTransaction>> GetHistoryByUser(long userId, OwnerHistoryQuery query, CancellationToken ct = default);

    /// <summary>Get a transaction by its database id, or <c>null</c> if none exists.</summary>
    Task<InvestmentTransaction?> Get(long id, CancellationToken cancellationToken = default);

    /// <summary>Get all transactions belonging to the given account.</summary>
    Task<IReadOnlyList<InvestmentTransaction>> GetByAccount(int accountId, CancellationToken cancellationToken = default);

    /// <summary>Get a user's transactions whose <see cref="InvestmentTransaction.TradeDate"/> falls within [startDate, endDate].</summary>
    Task<IReadOnlyList<InvestmentTransaction>> GetByUser(long userId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

    /// <summary>Insert a new transaction and return the persisted entity (with its generated id).</summary>
    Task<InvestmentTransaction> Add(InvestmentTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Update an existing transaction. Returns <c>false</c> when no transaction with that id exists.</summary>
    Task<bool> Update(InvestmentTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Delete the transaction with the given id. Returns <c>false</c> when no such transaction exists.</summary>
    Task<bool> Delete(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signed-quantity holdings per <see cref="AssetListing"/> as of a date, summed across the given accounts.
    /// Replaces the old per-ISIN running-balance boundary lookups with a single grouped query.
    /// </summary>
    Task<IReadOnlyDictionary<long, decimal>> GetHoldingsAsOf(IReadOnlyCollection<int> accountIds, DateOnly asOf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest transaction on or before a date for each listing held by an account, with
    /// listing and asset metadata eagerly loaded for holding-card rendering.
    /// </summary>
    Task<IReadOnlyList<InvestmentTransaction>> GetLatestByListingAsOf(int accountId, DateOnly asOf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets only the requested transactions belonging to an account, with the listing and asset
    /// metadata required for valuation. The account predicate is applied before materialisation.
    /// </summary>
    Task<IReadOnlyList<InvestmentTransaction>> GetByAccountAndIds(
        int accountId,
        IReadOnlyCollection<long> transactionIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Signed-quantity holdings per account and <see cref="AssetListing"/> as of a date for the given accounts,
    /// projected and aggregated at the database level.
    /// </summary>
    Task<IReadOnlyDictionary<int, IReadOnlyDictionary<long, decimal>>> GetHoldingsByAccountAsOf(IReadOnlyCollection<int> accountIds, DateOnly asOf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregated opening holdings before <paramref name="startDate"/> and daily signed-quantity deltas within
    /// [<paramref name="startDate"/>, <paramref name="endDate"/>] for the given accounts, projected and aggregated
    /// at the database boundary without loading out-of-range rows or full entity graphs.
    /// </summary>
    Task<AccountValuationInputs> GetValuationInputs(IReadOnlyCollection<int> accountIds, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bounded historical capital reconstruction inputs for transactions on or before <paramref name="toDate"/>
    /// across the given accounts. The projection includes the stable transaction/listing identity so
    /// the application can replay same-day history deterministically without loading entity graphs.
    /// </summary>
    Task<IReadOnlyList<CapitalFlowInput>> GetCapitalFlowInputs(IReadOnlyCollection<int> accountIds, DateOnly toDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// The distinct <see cref="AssetListing"/> ids referenced by any investment transaction across all
    /// users, resolved in a single grouped query. Feeds maintenance jobs (e.g. the weekly price
    /// backfill) that must cover every instrument anyone holds without materialising transactions.
    /// </summary>
    Task<IReadOnlyList<long>> GetDistinctAssetListingIds(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a bounded, deterministic page of investment transactions for an account, ordered by
    /// <see cref="InvestmentTransaction.TradeDate"/> descending then <see cref="InvestmentTransaction.Id"/> descending.
    /// Supports cursor continuation using (<paramref name="cursorTradeDate"/>, <paramref name="cursorId"/>)
    /// and optional date range, transaction type, and text search filters applied at the database level.
    /// Returns at most <paramref name="pageSize"/> items along with a boolean indicating whether more items exist.
    /// </summary>
    Task<(IReadOnlyList<InvestmentTransaction> Items, bool HasMore)> GetHistoryPage(
        int accountId,
        int pageSize,
        DateOnly? cursorTradeDate = null,
        long? cursorId = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        InvestmentTransactionType? type = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Owner-wide counterpart of the account <see cref="GetHistoryPage(int, int, DateOnly?, long?, DateOnly?, DateOnly?, InvestmentTransactionType?, string?, CancellationToken)"/>:
    /// every trade of <paramref name="userId"/>, linked to an account or not, with the same ordering, cursor and filters plus an optional listing.
    /// </summary>
    Task<(IReadOnlyList<InvestmentTransaction> Items, bool HasMore)> GetHistoryPageByUser(
        long userId,
        int pageSize,
        DateOnly? cursorTradeDate = null,
        long? cursorId = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        long? assetListingId = null,
        InvestmentTransactionType? type = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Non-zero signed units per listing as of a date, pooled across all of the owner's trades regardless of price or account,
    /// summed in the database and ordered by listing id. Oversold positions stay negative.
    /// </summary>
    Task<IReadOnlyList<InvestmentHolding>> GetHoldingsByUserAsOf(long userId, DateOnly asOf, CancellationToken cancellationToken = default);

    public sealed record ValuationTradeInput(
        int AccountId,
        long AssetListingId,
        DateOnly TradeDate,
        decimal SignedQuantity);

    public sealed record ValuationOpeningPosition(
        int AccountId,
        long AssetListingId,
        decimal Quantity);

    public sealed record AccountValuationInputs(
        IReadOnlyList<ValuationOpeningPosition> OpeningPositions,
        IReadOnlyList<ValuationTradeInput> InWindowTrades);

    public sealed record CapitalFlowInput(
        long TransactionId,
        int AccountId,
        long AssetListingId,
        DateOnly TradeDate,
        InvestmentTransactionType Type,
        decimal Quantity,
        decimal UnitPrice,
        decimal? Fee,
        string Currency,
        decimal? ListingPriceMultiplier);
}