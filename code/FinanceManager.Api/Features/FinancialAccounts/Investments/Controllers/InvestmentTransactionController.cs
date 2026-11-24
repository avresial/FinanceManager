using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Application.Alerts.Services;
using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Application.FinancialAccounts.Investments.Transactions;
using FinanceManager.Contracts.FinancialAccounts.Investments;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.ValueObjects;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.FinancialAccounts.Investments.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Investment Transactions")]
public class InvestmentTransactionController(
    IAccountRepository<InvestmentAccount> accountRepository,
    IInvestmentTransactionRepository transactionRepository,
    IAssetListingRepository assetListingRepository,
    IInvestmentInstrumentSearchService instrumentSearchService,
    IInvestmentPriceProvider priceProvider,
    InvestmentTransactionPriceRecoveryService priceRecoveryService) : ControllerBase
{
    [HttpGet("GetByAccount/{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<InvestmentTransactionDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByAccount(int accountId, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var transactions = await transactionRepository.GetByAccount(accountId, cancellationToken);
        await priceRecoveryService.RecoverMissingPricesAsync(transactions, account.UserId, cancellationToken);
        return Ok(transactions.Select(x => x.ToDto()).ToList());
    }

    /// <summary>
    /// Gets a bounded, filtered page of investment transactions for an account, ordered newest first
    /// with deterministic (TradeDate, Id) cursor continuation.
    /// </summary>
    [HttpGet("GetHistoryPage/{accountId:int}")]
    [HttpGet("History/{accountId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(InvestmentTransactionHistoryPageDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetHistoryPage(
        int accountId,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? cursor = null,
        [FromQuery] DateOnly? cursorTradeDate = null,
        [FromQuery] long? cursorId = null,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        [FromQuery] InvestmentTransactionType? type = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (pageSize <= 0) return BadRequest("pageSize must be greater than zero.");
        pageSize = Math.Clamp(pageSize, 1, 100);

        DateOnly? parsedCursorTradeDate = cursorTradeDate;
        long? parsedCursorId = cursorId;

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            if (!InvestmentHistoryCursor.TryCreate(cursor, out var parsedCursor))
                return BadRequest("Invalid cursor format.");

            parsedCursorTradeDate = parsedCursor.TradeDate;
            parsedCursorId = parsedCursor.Id;
        }

        if (parsedCursorTradeDate.HasValue != parsedCursorId.HasValue)
            return BadRequest("cursorTradeDate and cursorId must be provided together.");
        if (parsedCursorId is <= 0)
            return BadRequest("cursorId must be greater than zero.");

        var account = await accountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var (items, hasMore) = await transactionRepository.GetHistoryPage(
            accountId,
            pageSize,
            parsedCursorTradeDate,
            parsedCursorId,
            startDate,
            endDate,
            type,
            search,
            cancellationToken);

        await priceRecoveryService.RecoverMissingPricesAsync(items, account.UserId, cancellationToken);

        string? nextCursor = null;
        if (hasMore && items.Count > 0)
        {
            var last = items[^1];
            nextCursor = InvestmentHistoryCursor.Create(last.TradeDate, last.EntryId).ToString();
        }

        var dtos = items.Select(x => x.ToDto()).ToList();
        return Ok(new InvestmentTransactionHistoryPageDto(dtos, hasMore, nextCursor));
    }

    [HttpGet("Get/{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(InvestmentTransactionDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken = default)
    {
        var transaction = await transactionRepository.Get(id, cancellationToken);
        if (transaction?.AccountId is null) return NotFound();
        if (transaction.UserId is < int.MinValue or > int.MaxValue) return Forbid();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, (int)transaction.UserId)) return Forbid();

        return Ok(transaction.ToDto());
    }

    [HttpGet("SearchListings")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<InstrumentSearchResultDto>))]
    public async Task<IActionResult> SearchListings([FromQuery] string? q, [FromQuery] int maxResults = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(Array.Empty<InstrumentSearchResultDto>());
        maxResults = Math.Clamp(maxResults, 1, 20);
        var listings = await assetListingRepository.SearchAsync(q, maxResults, cancellationToken);
        return Ok(listings.Select(x => new InstrumentSearchResultDto(x.Id, x.Ticker, x.ExchangeName, x.TradingCurrency ?? string.Empty)).ToList());
    }

    [HttpGet("SearchInstruments")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<InvestmentInstrumentOptionDto>))]
    public async Task<IActionResult> SearchInstruments(
        [FromQuery] string? q,
        [FromQuery] int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(Array.Empty<InvestmentInstrumentOptionDto>());
        maxResults = Math.Clamp(maxResults, 1, 50);
        return Ok(await instrumentSearchService.SearchAsync(q, maxResults, cancellationToken));
    }

    [HttpGet("Listing/{listingId:long}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(InstrumentSearchResultDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetListing(long listingId, CancellationToken cancellationToken = default)
    {
        var listing = await assetListingRepository.Get(listingId, cancellationToken);
        return listing is null
            ? NotFound()
            : Ok(new InstrumentSearchResultDto(listing.Id, listing.Ticker, listing.ExchangeName, listing.TradingCurrency ?? string.Empty));
    }

    [HttpGet("ListingPrice/{listingId:long}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ListingPriceDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListingPrice(long listingId, [FromQuery] DateOnly? asOf = null, CancellationToken cancellationToken = default)
    {
        var listing = await assetListingRepository.Get(listingId, cancellationToken);
        if (listing is null) return NotFound();
        if (string.IsNullOrWhiteSpace(listing.TradingCurrency))
            return Ok(new ListingPriceDto(null, string.Empty, "Automatic prices aren't available until the listing currency is configured.", null));

        var currency = new Currency(0, listing.TradingCurrency, listing.TradingCurrency);
        var priceDate = asOf?.ToDateTime(TimeOnly.MinValue) ?? DateTime.UtcNow;
        var result = await priceProvider.GetPricePerUnitResultAsync(listingId, currency, priceDate, cancellationToken);
        return Ok(result switch
        {
            InvestmentPriceResult.Success(var price) => new ListingPriceDto(price, listing.TradingCurrency),
            InvestmentPriceResult.NotYetPublished(var message, var retryAtUtc) => new ListingPriceDto(null, listing.TradingCurrency, message, retryAtUtc),
            InvestmentPriceResult.NotFound => new ListingPriceDto(null, listing.TradingCurrency),
        });
    }
}