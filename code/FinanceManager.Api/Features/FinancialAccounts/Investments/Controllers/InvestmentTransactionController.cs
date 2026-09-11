using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Application.FinancialAccounts.Investments.Transactions;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text;

namespace FinanceManager.Api.Features.FinancialAccounts.Investments.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Investment Transactions")]
public class InvestmentTransactionController(
    IAccountRepository<InvestmentAccount> accountRepository,
    IInvestmentTransactionRepository transactionRepository,
    IInvestmentTransactionService transactionService,
    ICacheInvalidator dashboardCacheInvalidator,
    IAssetListingRepository assetListingRepository,
    IInvestmentInstrumentSearchService instrumentSearchService,
    IInvestmentPriceProvider priceProvider,
    ILogger<InvestmentTransactionController> logger) : ControllerBase
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
        await RecoverMissingPricesAsync(transactions, account.UserId, cancellationToken);
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
            if (!TryParseCursor(cursor, out var cDate, out var cId))
                return BadRequest("Invalid cursor format.");

            parsedCursorTradeDate = cDate;
            parsedCursorId = cId;
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

        await RecoverMissingPricesAsync(items, account.UserId, cancellationToken);

        string? nextCursor = null;
        if (hasMore && items.Count > 0)
        {
            var last = items[^1];
            nextCursor = FormatCursor(last.TradeDate, last.Id);
        }

        var dtos = items.Select(x => x.ToDto()).ToList();
        return Ok(new InvestmentTransactionHistoryPageDto(dtos, hasMore, nextCursor));
    }

    /// <summary>
    /// Gets one as-of transaction per currently held listing. The response is small even when an
    /// account has a large history, and supplies the metadata needed to render holding cards beside
    /// the independently calculated quantities.
    /// </summary>
    [HttpGet("GetHoldingMetadata/{accountId:int}/{date:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<InvestmentTransactionDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetHoldingMetadata(int accountId, DateTime date, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var asOf = DateOnly.FromDateTime(date);
        var holdings = await transactionRepository.GetHoldingsAsOf([accountId], asOf, cancellationToken);
        var latestTransactions = await transactionRepository.GetLatestByListingAsOf(accountId, asOf, cancellationToken);
        var metadata = latestTransactions
            .Where(transaction => holdings.TryGetValue(transaction.AssetListingId, out var quantity) && quantity != 0m)
            .ToList();

        await RecoverMissingPricesAsync(metadata, account.UserId, cancellationToken);
        return Ok(metadata.Select(x => x.ToDto()).ToList());
    }

    private async Task RecoverMissingPricesAsync(
        IReadOnlyList<InvestmentTransaction> transactions,
        int userId,
        CancellationToken cancellationToken)
    {
        var recoveredAny = false;
        var missingPrices = transactions
            .Where(x => x.UnitPrice <= 0m && !string.IsNullOrWhiteSpace(x.Currency))
            .GroupBy(x => new { x.AssetListingId, x.Currency, x.TradeDate });

        foreach (var group in missingPrices)
        {
            decimal price;
            try
            {
                var currency = new Currency(0, group.Key.Currency, group.Key.Currency);
                price = await priceProvider.GetPricePerUnitAsync(
                    group.Key.AssetListingId,
                    currency,
                    group.Key.TradeDate.ToDateTime(TimeOnly.MinValue),
                    cancellationToken);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug(ex, "Price recovery cancelled for listing {ListingId} on {TradeDate}.", group.Key.AssetListingId, group.Key.TradeDate);
                throw;
            }
            catch (OperationCanceledException ex)
            {
                logger.LogDebug(ex, "Price recovery cancelled or timed out for listing {ListingId} on {TradeDate}; continuing.", group.Key.AssetListingId, group.Key.TradeDate);
                continue;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to recover price for listing {ListingId} on {TradeDate}", group.Key.AssetListingId, group.Key.TradeDate);
                continue;
            }

            if (price <= 0m) continue;

            // ponytail: one save per recovered trade; batch updates only if accounts accumulate a large backlog.
            foreach (var transaction in group)
            {
                var previousPrice = transaction.UnitPrice;
                transaction.UnitPrice = price;
                if (await transactionRepository.Update(transaction, cancellationToken))
                    recoveredAny = true;
                else
                    transaction.UnitPrice = previousPrice;
            }
        }

        if (recoveredAny)
            await dashboardCacheInvalidator.InvalidateUser(userId);
    }

    [HttpGet("Get/{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(InvestmentTransactionDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken = default)
    {
        var transaction = await transactionRepository.Get(id, cancellationToken);
        if (transaction is null) return NotFound();
        if (transaction.UserId is < int.MinValue or > int.MaxValue) return Forbid();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, (int)transaction.UserId)) return Forbid();

        return Ok(transaction.ToDto());
    }

    [HttpPost("Add")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(InvestmentTransactionDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Add(AddInvestmentTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.Get(request.AccountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        try
        {
            return Ok(await transactionService.AddAsync(request, account.UserId, cancellationToken));
        }
        catch (InvestmentTransactionCommandException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Could not save the transaction.",
                detail: ex.Message,
                type: $"https://financemanager.dev/problems/{ex.Code}",
                extensions: new Dictionary<string, object?> { ["code"] = ex.Code });
        }
    }

    [HttpPut("Update")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(UpdateInvestmentTransactionRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsValid(request.AssetListingId, request.Quantity, request.UnitPrice, request.Currency, request.TradeDate))
            return BadRequest("Invalid input parameters.");

        var account = await accountRepository.Get(request.AccountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var existing = await transactionRepository.Get(request.Id, cancellationToken);
        if (existing is null || existing.AccountId != request.AccountId) return NotFound();

        var result = await transactionRepository.Update(request.ToEntity(), cancellationToken);
        await dashboardCacheInvalidator.InvalidateUser(account.UserId);
        return Ok(result);
    }

    [HttpDelete("Delete/{accountId:int}/{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int accountId, long id, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.Get(accountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var existing = await transactionRepository.Get(id, cancellationToken);
        if (existing is null || existing.AccountId != accountId) return NotFound();

        var result = await transactionRepository.Delete(id, cancellationToken);
        await dashboardCacheInvalidator.InvalidateUser(account.UserId);
        return Ok(result);
    }

    [HttpGet("SearchListings")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<InstrumentSearchResultDto>))]
    public async Task<IActionResult> SearchListings([FromQuery] string? q, [FromQuery] int maxResults = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(Array.Empty<InstrumentSearchResultDto>());
        maxResults = Math.Clamp(maxResults, 1, 20);
        var listings = await assetListingRepository.SearchAsync(q, maxResults, cancellationToken);
        return Ok(listings.Select(x => new InstrumentSearchResultDto(x.Id, x.Ticker, x.ExchangeName, x.TradingCurrency)).ToList());
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
            : Ok(new InstrumentSearchResultDto(listing.Id, listing.Ticker, listing.ExchangeName, listing.TradingCurrency));
    }

    [HttpGet("ListingPrice/{listingId:long}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ListingPriceDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListingPrice(long listingId, [FromQuery] DateOnly? asOf = null, CancellationToken cancellationToken = default)
    {
        var listing = await assetListingRepository.Get(listingId, cancellationToken);
        if (listing is null) return NotFound();
        var currency = new Currency(0, listing.TradingCurrency, listing.TradingCurrency);
        var priceDate = asOf?.ToDateTime(TimeOnly.MinValue) ?? DateTime.UtcNow;
        var result = await priceProvider.GetPricePerUnitResultAsync(listingId, currency, priceDate, cancellationToken);
        return Ok(new ListingPriceDto(result.Price > 0 ? result.Price : null, listing.TradingCurrency, result.Message, result.RetryAtUtc));
    }

    private static bool IsValid(long assetListingId, decimal quantity, decimal unitPrice, string? currency, DateOnly tradeDate) =>
        assetListingId > 0 && quantity > 0 && unitPrice >= 0 && !string.IsNullOrWhiteSpace(currency) && tradeDate != default;

    private static string FormatCursor(DateOnly tradeDate, long id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{tradeDate:yyyy-MM-dd}:{id}"));

    private static bool TryParseCursor(string cursor, out DateOnly tradeDate, out long id)
    {
        tradeDate = default;
        id = 0;
        if (string.IsNullOrWhiteSpace(cursor)) return false;

        var raw = cursor.Trim();
        try
        {
            var bytes = Convert.FromBase64String(raw);
            var decoded = Encoding.UTF8.GetString(bytes);
            if (TryParseDelimiter(decoded, out tradeDate, out id))
                return true;
        }
        catch
        {
            // Fall back to plain text delimiter parsing
        }

        return TryParseDelimiter(raw, out tradeDate, out id);
    }

    private static bool TryParseDelimiter(string text, out DateOnly tradeDate, out long id)
    {
        tradeDate = default;
        id = 0;
        var parts = text.Split([':', '|', '_'], 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 &&
            DateOnly.TryParse(parts[0], out tradeDate) &&
            long.TryParse(parts[1], out id) &&
            id > 0)
        {
            return true;
        }
        return false;
    }
}