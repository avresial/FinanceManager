using FinanceManager.Api.Shared.Helpers;
using FinanceManager.Application.Portfolio.Valuation;
using FinanceManager.Contracts.Portfolio;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.Currencies.Repositories;
using FinanceManager.Domain.Portfolio.Valuation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.Portfolio.Controllers;

/// <summary>
/// Values the caller's pooled investment holdings: every trade of the authenticated user counts, whether or not it is linked to an account.
/// The owner comes from the authenticated principal only.
/// </summary>
[Authorize]
[Route("api/[controller]")]
[ApiController]
[Tags("Investment Valuation")]
public class InvestmentValuationController(
    IInvestmentValuationService valuationService,
    IInvestmentBenchmarkService benchmarkService,
    IInvestmentCapitalSeriesService capitalSeriesService,
    IInvestmentTransactionValuationService transactionValuationService,
    ICurrencyRepository currencyRepository,
    IAssetListingRepository assetListingRepository) : ControllerBase
{
    /// <summary>The most trades one valuation request may name; the largest caller page is 100.</summary>
    public const int MaxTransactionValuationIds = 200;

    [HttpGet("Value/{currencyId:int}/{date:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(decimal))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetValue(int currencyId, DateTime date, CancellationToken cancellationToken = default)
    {
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var userId)) return Forbid();

        var currency = await currencyRepository.GetCurrency(currencyId, cancellationToken);
        if (currency is null) return NotFound("Currency not found.");

        var values = await valuationService.GetUserValueByListingAsync(userId, currency, date, cancellationToken);
        return Ok(values.Values.Sum());
    }

    [HttpGet("ValueSeries/{currencyId:int}/{startDate:DateTime}/{endDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyDictionary<DateTime, decimal>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetValueSeries(int currencyId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        if (endDate < startDate) return BadRequest("End date must be on or after start date.");
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var userId)) return Forbid();

        var currency = await currencyRepository.GetCurrency(currencyId, cancellationToken);
        if (currency is null) return NotFound("Currency not found.");

        return Ok(await valuationService.GetUserValueSeriesAsync(userId, currency, startDate, endDate, cancellationToken));
    }

    [HttpGet("CapitalSeries/{currencyId:int}/{startDate:DateTime}/{endDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyDictionary<DateTime, decimal>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCapitalSeries(int currencyId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        if (endDate < startDate) return BadRequest("End date must be on or after start date.");
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var userId)) return Forbid();

        var currency = await currencyRepository.GetCurrency(currencyId, cancellationToken);
        if (currency is null) return NotFound("Currency not found.");

        return Ok(await capitalSeriesService.GetCapitalSeriesAsync(userId, currency, startDate, endDate, cancellationToken));
    }

    [HttpGet("BenchmarkSeries/{currencyId:int}/{startDate:DateTime}/{endDate:DateTime}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyDictionary<DateTime, decimal>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetBenchmarkSeries(
        int currencyId,
        DateTime startDate,
        DateTime endDate,
        [FromQuery] long? listingId = null,
        CancellationToken cancellationToken = default)
    {
        if (endDate < startDate) return BadRequest("End date must be on or after start date.");
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var userId)) return Forbid();

        var currency = await currencyRepository.GetCurrency(currencyId, cancellationToken);
        if (currency is null) return NotFound("Currency not found.");
        if (listingId is long id && await assetListingRepository.Get(id, cancellationToken) is null)
            return NotFound("Investment benchmark not found.");

        return Ok(await benchmarkService.GetBenchmarkSeriesAsync(
            listingId, userId, currency, startDate, endDate, cancellationToken));
    }

    /// <summary>Valuations of the Buy trades named by <paramref name="transactionIds"/> (1 to <see cref="MaxTransactionValuationIds"/>); other users' ids are ignored.</summary>
    [HttpGet("TransactionValuations/{currencyId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<InvestmentTransactionValuationDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTransactionValuations(
        int currencyId,
        [FromQuery] long[]? transactionIds = null,
        CancellationToken cancellationToken = default)
    {
        if (transactionIds is null || transactionIds.Length == 0)
            return BadRequest("At least one transaction id is required.");
        if (transactionIds.Length > MaxTransactionValuationIds)
            return BadRequest($"At most {MaxTransactionValuationIds} transaction ids can be valued per request.");
        if (!ApiAuthenticationHelper.TryGetOwnerId(User, out var userId)) return Forbid();

        var currency = await currencyRepository.GetCurrency(currencyId, cancellationToken);
        if (currency is null) return NotFound("Currency not found.");

        return Ok(await transactionValuationService.GetForUserAsync(userId, currency, transactionIds, cancellationToken));
    }
}