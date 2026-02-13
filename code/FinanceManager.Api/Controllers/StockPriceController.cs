using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Tags("Stock Prices")]
public class StockPriceController(IStockPriceRepository stockPriceRepository, ICurrencyExchangeService currencyExchangeService,
ICurrencyRepository currencyRepository, IStockMarketService stockMarketService, IStockDetailsRepository stockDetailsRepository) : ControllerBase
{

    [Authorize]
    [HttpPost("add-stock-price")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StockPrice))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddStockPrice([FromQuery] string ticker, [FromQuery] decimal pricePerUnit, [FromQuery] int currencyId, [FromQuery] DateTime date, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker) || pricePerUnit <= 0 || date == default)
            return BadRequest("Invalid input parameters.");

        var currency = await currencyRepository.GetCurrency(currencyId, cancellationToken);
        if (currency is null) return NotFound("Currency not found.");

        var stockPrice = await stockPriceRepository.Add(ticker.ToUpper(), pricePerUnit, currency, date);
        return Ok(stockPrice);
    }

    [Authorize]
    [HttpPost("update-stock-price")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StockPrice))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStockPrice([FromQuery] string ticker, [FromQuery] decimal pricePerUnit, [FromQuery] int currencyId, [FromQuery] DateTime date, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker) || pricePerUnit <= 0 || date == default)
            return BadRequest("Invalid input parameters.");

        var currency = await currencyRepository.GetCurrency(currencyId, cancellationToken);
        if (currency is null) return NotFound("Currency not found.");

        var stockPrice = await stockPriceRepository.Update(ticker.ToUpper(), pricePerUnit, currency, date);
        return Ok(stockPrice);
    }

    [HttpGet("get-stock-price")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StockPrice))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStockPrice([FromQuery] string ticker, [FromQuery] int currencyId, [FromQuery] DateTime date, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker) || date == default)
            return BadRequest("Invalid input parameters.");

        var currency = await currencyRepository.GetCurrency(currencyId, cancellationToken);
        if (currency is null)
            return NotFound("Currency not found.");

        var stockPrices = await stockMarketService.GetDailyStock(ticker, date, date, cancellationToken);
        if (stockPrices.Count == 0) return NotFound("Stock price not found.");
        var stockPrice = stockPrices.First(sp => sp.Date.Date == date.Date);
        if (currency == stockPrice.Currency)
            return Ok(stockPrice);

        var exchangeRate = await currencyExchangeService.GetExchangeRateAsync(stockPrice.Currency, currency, date); // TODO add cache
        if (exchangeRate is null)
            return NotFound($"Exchange rate from {stockPrice.Currency} to {currency} not found for the specified date.");

        stockPrice.PricePerUnit = stockPrice.PricePerUnit * exchangeRate.Value;
        stockPrice.Currency = currency;

        return Ok(stockPrice);
    }


    [HttpGet("get-stock-prices")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<StockPrice>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStockPrices([FromQuery] string ticker, [FromQuery] DateTime start, [FromQuery] DateTime end, [FromQuery] long step = default, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker) || start == default || end == default || step == default)
            return BadRequest("Invalid input parameters.");

        List<StockPrice> stockPrices = [];

        for (var i = start; i < end; i = i.Add(new(step)))
        {
            var stockPrice = await stockPriceRepository.Get(ticker, i);
            if (stockPrice is null) continue;
            stockPrices.Add(stockPrice);
        }

        if (!stockPrices.Any()) return NotFound("Stock prices not found.");

        return Ok(stockPrices);
    }

    [HttpGet("get-latest-missing-stock-price")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StockPrice))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestMissingStockPrice(string ticker, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            return BadRequest("Invalid input parameters.");

        var stockPrice = await stockPriceRepository.GetLatestMissing(ticker);

        if (stockPrice is null) return NotFound("Stock price not found.");

        return Ok(stockPrice);
    }

    [HttpGet("get-ticker-currency")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TickerCurrency))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTickerCurrency(string ticker, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            return BadRequest("Invalid input parameters.");

        var currency = await stockPriceRepository.GetTickerCurrency(ticker);

        if (currency is null) return NotFound("Stock price not found.");

        return Ok(new TickerCurrency(ticker, currency));
    }

    [HttpGet("get-stocks")]
    // [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<StockDetails>))]
    public async Task<IActionResult> GetStocks(CancellationToken cancellationToken = default)
        => Ok(await stockDetailsRepository.GetAll(cancellationToken));

    [HttpGet("stock-details/{ticker}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StockDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStockDetails(string ticker, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            return BadRequest("Invalid input parameters.");

        var details = await stockDetailsRepository.Get(ticker, cancellationToken);
        if (details is null) return NotFound();

        return Ok(details);
    }

    public sealed record AddStockRequest(string Ticker, string Name, string Type, string Region, string Currency);
    public sealed record UpdateStockRequest(string Ticker, string Name, string Type, string Region, string Currency);

    [HttpPost("add-stock")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StockDetails))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddStock([FromBody] AddStockRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Ticker) || string.IsNullOrWhiteSpace(request.Currency))
            return BadRequest("Invalid input parameters.");
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Type) || string.IsNullOrWhiteSpace(request.Region))
            return BadRequest("Invalid input parameters.");

        var normalizedCurrency = request.Currency.Trim().ToUpperInvariant();
        var currency = await currencyRepository.GetOrAdd(normalizedCurrency, normalizedCurrency, cancellationToken);
        var details = new StockDetails
        {
            Ticker = request.Ticker.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            Type = request.Type.Trim(),
            Region = request.Region.Trim(),
            Currency = currency
        };

        var result = await stockDetailsRepository.Add(details, cancellationToken);

        return Ok(result);
    }

    [HttpPut("stock-details")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StockDetails))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateStockDetails([FromBody] UpdateStockRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Ticker) || string.IsNullOrWhiteSpace(request.Currency))
            return BadRequest("Invalid input parameters.");
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Type) || string.IsNullOrWhiteSpace(request.Region))
            return BadRequest("Invalid input parameters.");

        var normalizedCurrency = request.Currency.Trim().ToUpperInvariant();
        var currency = await currencyRepository.GetOrAdd(normalizedCurrency, normalizedCurrency, cancellationToken);
        var details = new StockDetails
        {
            Ticker = request.Ticker.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            Type = request.Type.Trim(),
            Region = request.Region.Trim(),
            Currency = currency
        };

        var result = await stockDetailsRepository.Add(details, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("delete-stock/{ticker}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteStock(string ticker, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            return BadRequest("Invalid input parameters.");

        if (!await stockDetailsRepository.Delete(ticker, cancellationToken))
            return NotFound();

        return NoContent();
    }

    [HttpGet("search-ticker")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<TickerSearchMatch>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SearchTicker([FromQuery] string keywords, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keywords))
            return BadRequest("Invalid input parameters.");

        var result = await stockMarketService.SearchTicker(keywords, cancellationToken);
        if (result.Count == 0) return NotFound("No ticker matches found.");

        return Ok(result);
    }

    [HttpGet("get-daily-stock")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<StockPrice>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDailyStock([FromQuery] string ticker, [FromQuery] DateTime start, [FromQuery] DateTime end, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker) || start == default || end == default)
            return BadRequest("Invalid input parameters.");
        if (end < start)
            return BadRequest("End date must be after start date.");

        var result = await stockMarketService.GetDailyStock(ticker, start, end, cancellationToken);
        if (result.Count == 0) return NotFound("Stock prices not found.");

        return Ok(result);
    }
}