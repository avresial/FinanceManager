using FinanceManager.Api.Helpers;
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

namespace FinanceManager.Api.Controllers.Accounts;

[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Investment Transactions")]
public class InvestmentTransactionController(
    IAccountRepository<InvestmentAccount> accountRepository,
    IInvestmentTransactionRepository transactionRepository,
    ICacheInvalidator dashboardCacheInvalidator,
    IAssetListingRepository assetListingRepository,
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
        if (!IsValid(request.AssetListingId, request.Quantity, request.UnitPrice, request.Currency, request.TradeDate))
            return BadRequest("Invalid input parameters.");

        var account = await accountRepository.Get(request.AccountId);
        if (account is null) return NotFound();
        if (!ApiAuthenticationHelper.IsAccountOwner(User, account.UserId)) return Forbid();

        var result = await transactionRepository.Add(request.ToEntity(account.UserId), cancellationToken);
        await dashboardCacheInvalidator.InvalidateUser(account.UserId);
        return Ok(result.ToDto());
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

    [HttpGet("ListingPrice/{listingId:long}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ListingPriceDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListingPrice(long listingId, [FromQuery] DateOnly? asOf = null, CancellationToken cancellationToken = default)
    {
        var listing = await assetListingRepository.Get(listingId, cancellationToken);
        if (listing is null) return NotFound();
        var currency = new Currency(0, listing.TradingCurrency, listing.TradingCurrency);
        var priceDate = asOf?.ToDateTime(TimeOnly.MinValue) ?? DateTime.UtcNow;
        var price = await priceProvider.GetPricePerUnitAsync(listingId, currency, priceDate, cancellationToken);
        return Ok(new ListingPriceDto(price > 0 ? price : null, listing.TradingCurrency));
    }

    private static bool IsValid(long assetListingId, decimal quantity, decimal unitPrice, string? currency, DateOnly tradeDate) =>
        assetListingId > 0 && quantity > 0 && unitPrice >= 0 && !string.IsNullOrWhiteSpace(currency) && tradeDate != default;
}