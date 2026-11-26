using FinanceManager.Application.Assets.Discovery;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.Assets.Services;
using FinanceManager.Domain.Currencies.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.Assets.Controllers;

/// <summary>
/// User-facing instrument discovery over external providers (OpenFIGI + Alpha Vantage), kept
/// separate from the admin asset CRUD API.
/// </summary>
[Authorize]
[ApiController]
[Route("api/investments/instruments")]
[Tags("Investment Instruments")]
public class InvestmentInstrumentDiscoveryController(
    IInvestmentInstrumentDiscoveryService discoveryService,
    IInstrumentImportService importService,
    IAssetListingRepository assetListingRepository,
    IInvestmentInstrumentSearchService instrumentSearchService) : ControllerBase
{
    [HttpGet("search")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<InstrumentDiscoveryResultDto>))]
    public async Task<IActionResult> Search([FromQuery] string? query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Ok(Array.Empty<InstrumentDiscoveryResultDto>());

        var results = await discoveryService.SearchAsync(query, cancellationToken);
        return Ok(results);
    }

    [HttpPost("import-preview")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(InstrumentImportPreviewDto))]
    public async Task<IActionResult> ImportPreview([FromBody] InstrumentDiscoveryResultDto instrument, CancellationToken cancellationToken) =>
        Ok(await importService.GetImportPreviewAsync(instrument, cancellationToken));

    [HttpPost("import")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ImportedInstrumentDto))]
    public async Task<IActionResult> Import([FromBody] ImportInstrumentCommand command, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await importService.ImportAsync(command, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("listings")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<InstrumentSearchResultDto>))]
    public async Task<IActionResult> SearchListings([FromQuery] string? q, [FromQuery] int maxResults = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(Array.Empty<InstrumentSearchResultDto>());
        maxResults = Math.Clamp(maxResults, 1, 20);
        var listings = await assetListingRepository.SearchAsync(q, maxResults, cancellationToken);
        return Ok(listings.Select(x => new InstrumentSearchResultDto(x.Id, x.Ticker, x.ExchangeName, x.TradingCurrency ?? string.Empty)).ToList());
    }

    [HttpGet("options")]
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

    [HttpGet("listings/{listingId:long}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(InstrumentSearchResultDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetListing(long listingId, CancellationToken cancellationToken = default)
    {
        var listing = await assetListingRepository.Get(listingId, cancellationToken);
        return listing is null
            ? NotFound()
            : Ok(new InstrumentSearchResultDto(listing.Id, listing.Ticker, listing.ExchangeName, listing.TradingCurrency ?? string.Empty));
    }

    [HttpGet("listings/{listingId:long}/price")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ListingPriceDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListingPrice(long listingId, [FromServices] IInvestmentPriceProvider priceProvider, [FromQuery] DateOnly? asOf = null, CancellationToken cancellationToken = default)
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