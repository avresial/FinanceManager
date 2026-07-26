using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.Assets.Controllers;

/// <summary>
/// Admin management API for the investment asset master data: assets, their exchange listings,
/// and provider-specific market data symbols. Admin-only; backed directly by the repositories.
/// </summary>
[Route("api/admin/assets")]
[Authorize(Roles = "Admin")]
[ApiController]
[Tags("Admin - Assets")]
public class AssetController(
    IAssetRepository assetRepository,
    IAssetListingRepository listingRepository,
    IMarketDataSymbolRepository symbolRepository,
    IPriceQuoteRepository priceQuoteRepository,
    ICurrencyRepository currencyRepository) : ControllerBase
{
    // ----- Assets -----

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<AssetDto>))]
    public async Task<IActionResult> GetAssets(CancellationToken cancellationToken)
    {
        var assets = await assetRepository.GetAll(cancellationToken);
        return Ok(assets.Select(a => a.ToDto()).ToList());
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AssetDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsset(long id, CancellationToken cancellationToken)
    {
        var asset = await assetRepository.Get(id, cancellationToken);
        return asset is null ? NotFound() : Ok(asset.ToDto());
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(AssetDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAsset([FromBody] AssetDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Asset name is required.");
        if (!await IsKnownCurrency(dto.BaseCurrency, cancellationToken))
            return BadRequest("Base currency must be one of the predefined currencies.");

        var created = await assetRepository.Add(dto.ToNewEntity(), cancellationToken);
        return CreatedAtAction(nameof(GetAsset), new { id = created.Id }, created.ToDto());
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsset(long id, [FromBody] AssetDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Asset name is required.");
        if (!await IsKnownCurrency(dto.BaseCurrency, cancellationToken))
            return BadRequest("Base currency must be one of the predefined currencies.");

        var entity = dto.ToNewEntity();
        entity.Id = id;
        var updated = await assetRepository.Update(entity, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsset(long id, CancellationToken cancellationToken)
    {
        var deleted = await assetRepository.Delete(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    // ----- Listings -----

    [HttpPost("{assetId:long}/listings")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(AssetListingDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateListing(long assetId, [FromBody] AssetListingDto dto, CancellationToken cancellationToken)
    {
        if (ValidateListing(dto) is string error)
            return BadRequest(error);
        if (!await IsKnownTradingCurrency(dto.TradingCurrency, cancellationToken))
            return BadRequest("Trading currency must be one of the predefined currencies.");
        if (await assetRepository.Get(assetId, cancellationToken) is null)
            return NotFound("Asset not found.");

        var created = await listingRepository.Add(dto.ToNewEntity(assetId), cancellationToken);
        return CreatedAtAction(nameof(GetAsset), new { id = assetId }, created.ToDto());
    }

    [HttpPut("listings/{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateListing(long id, [FromBody] AssetListingDto dto, CancellationToken cancellationToken)
    {
        if (ValidateListing(dto) is string error)
            return BadRequest(error);
        if (!await IsKnownTradingCurrency(dto.TradingCurrency, cancellationToken))
            return BadRequest("Trading currency must be one of the predefined currencies.");

        var existing = await listingRepository.Get(id, cancellationToken);
        if (existing is null) return NotFound();

        dto.ApplyTo(existing);
        var updated = await listingRepository.Update(existing, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("listings/{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteListing(long id, CancellationToken cancellationToken)
    {
        var deleted = await listingRepository.Delete(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    // ----- Market data symbols -----

    [HttpPost("listings/{listingId:long}/symbols")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(MarketDataSymbolDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateSymbol(long listingId, [FromBody] MarketDataSymbolDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Symbol))
            return BadRequest("Symbol is required.");
        var listing = await listingRepository.Get(listingId, cancellationToken);
        if (listing is null)
            return NotFound("Listing not found.");

        var created = await symbolRepository.Add(dto.ToNewEntity(listingId), cancellationToken);
        return CreatedAtAction(nameof(GetAsset), new { id = listing.AssetId }, created.ToDto());
    }

    [HttpPut("symbols/{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSymbol(long id, [FromBody] MarketDataSymbolDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Symbol))
            return BadRequest("Symbol is required.");

        var existing = await symbolRepository.Get(id, cancellationToken);
        if (existing is null) return NotFound();

        dto.ApplyTo(existing);
        var updated = await symbolRepository.Update(existing, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("symbols/{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSymbol(long id, CancellationToken cancellationToken)
    {
        var deleted = await symbolRepository.Delete(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    // ----- Manual prices -----

    [HttpPost("listings/{listingId:long}/prices/manual")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddManualPrice(
        long listingId,
        [FromBody] ManualPriceRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Price <= 0m)
            return BadRequest("Price must be greater than zero.");
        if (request.Date > DateTimeOffset.UtcNow)
            return BadRequest("Price date cannot be in the future.");

        var listing = await listingRepository.Get(listingId, cancellationToken);
        if (listing is null)
            return NotFound("Listing not found.");

        var priceQuote = new PriceQuote
        {
            AssetListingId = listingId,
            Provider = MarketDataProvider.Manual,
            QuoteType = PriceQuoteType.Manual,
            Price = request.Price,
            Currency = listing.TradingCurrency,
            PriceTime = new DateTimeOffset(request.Date.UtcDateTime.Date, TimeSpan.Zero),
            RawPrice = request.Price,
            RawCurrency = listing.TradingCurrency,
            FetchedAt = DateTimeOffset.UtcNow
        };

        await priceQuoteRepository.Upsert(priceQuote, cancellationToken);
        return Ok();
    }

    // Currency codes must come from the predefined list; free-typed codes are rejected. An empty
    // base currency is allowed because it is optional on an asset.
    private async Task<bool> IsKnownCurrency(string? currency, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currency)) return true;
        return await currencyRepository.GetByCode(currency, cancellationToken) is not null;
    }

    // Listings additionally accept minor "pence"-style quote units (e.g. GBX) that exchanges use
    // as trading currencies but that are not standalone currencies.
    private async Task<bool> IsKnownTradingCurrency(string? currency, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(currency) && DefaultCurrency.MinorQuoteUnits.ContainsKey(currency.Trim()))
            return true;

        return await IsKnownCurrency(currency, cancellationToken);
    }

    private static string? ValidateListing(AssetListingDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Ticker)) return "Ticker is required.";
        if (string.IsNullOrWhiteSpace(dto.ExchangeMic)) return "Exchange MIC is required.";
        if (string.IsNullOrWhiteSpace(dto.TradingCurrency)) return "Trading currency is required.";
        if (dto.PriceMultiplier <= 0m) return "Price multiplier must be greater than zero.";
        return null;
    }
}