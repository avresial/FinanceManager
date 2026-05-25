using FinanceManager.Application.Services.Stocks;
using FinanceManager.Domain.Commands.Stocks;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Tags("Stock Prices")]
public partial class StockPriceController(IStockPriceRepository stockPriceRepository, ICurrencyExchangeService currencyExchangeService,
ICurrencyRepository currencyRepository, IStockMarketService stockMarketService, IStockPriceProvider stockPriceProvider, IStockDetailsRepository stockDetailsRepository,
IStockPriceBulkImportService stockPriceBulkImportService) : ControllerBase
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

        var normalizedTicker = ticker.Trim().ToUpperInvariant();

        var currency = await currencyRepository.GetCurrency(currencyId, cancellationToken);
        if (currency is null)
            return NotFound("Currency not found.");

        var stockPrice = await stockPriceRepository.GetThisOrNextOlder(normalizedTicker, date);
        if (stockPrice is null)
        {
            var fetchedPrice = await stockPriceProvider.GetPricePerUnitAsync(normalizedTicker, currency, date);
            if (fetchedPrice <= 0)
                return NotFound("Stock price not found.");

            stockPrice = await stockPriceRepository.GetThisOrNextOlder(normalizedTicker, date);
            if (stockPrice is null)
            {
                return Ok(new StockPrice
                {
                    Ticker = normalizedTicker,
                    PricePerUnit = fetchedPrice,
                    Currency = currency,
                    Date = date.Date
                });
            }
        }

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
    public async Task<IActionResult> GetStockPrices([FromQuery] string ticker, [FromQuery] int currencyId, [FromQuery] DateTime start, [FromQuery] DateTime end, [FromQuery] long step = default, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker) || start == default || end == default)
            return BadRequest("Invalid input parameters.");

        var currency = await currencyRepository.GetCurrency(currencyId, cancellationToken);
        if (currency is null)
            return NotFound("Currency not found.");

        var stockPrices = await stockMarketService.GetStockPrices(ticker, start, end, cancellationToken);

        if (stockPrices.Count == 0)
            return NotFound("Stock prices not found.");

        Dictionary<DateTime, decimal?> ratesByDate = [];
        var sourceCurrency = stockPrices.First().Currency;
        if (sourceCurrency != currency)
        {
            var rangeRates = await currencyExchangeService.GetExchangeRateAsync(sourceCurrency, currency, start, end);
            ratesByDate = rangeRates
                .GroupBy(x => x.Date.Date)
                .ToDictionary(x => x.Key, x => x.Last().Value);
        }

        List<StockPrice> result = [];
        foreach (var price in stockPrices)
        {
            if (price.Currency == currency)
                result.Add(price);
            else
            {
                if (ratesByDate.TryGetValue(price.Date.Date, out var exchangeRate)
                    && exchangeRate is not null)
                {
                    var convertedPrice = new StockPrice
                    {
                        Ticker = price.Ticker,
                        PricePerUnit = price.PricePerUnit * exchangeRate.Value,
                        Currency = currency,
                        Date = price.Date
                    };
                    result.Add(convertedPrice);
                }
            }
        }

        if (step > 0)
        {
            var resultByDate = result
                .GroupBy(sp => sp.Date.Date)
                .ToDictionary(group => group.Key, group => group.OrderBy(sp => sp.Date).Last());

            List<StockPrice> filteredPrices = [];
            for (var i = start; i <= end; i = i.Add(new TimeSpan(step)))
            {
                if (resultByDate.TryGetValue(i.Date, out var priceOnDate))
                    filteredPrices.Add(priceOnDate);
            }
            return Ok(filteredPrices);
        }

        return Ok(result);
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


    [HttpGet("get-stock-details/{ticker}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StockDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStockDetails(string ticker, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            return BadRequest("Invalid input parameters.");

        var details = await stockDetailsRepository.Get(ticker, cancellationToken);
        if (details is null)
            return NotFound();

        return Ok(details);
    }

    [HttpGet("get-stocks-details")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<StockDetails>))]
    public async Task<IActionResult> GetStocksDetails(CancellationToken cancellationToken = default)
        => Ok(await stockDetailsRepository.GetAll(cancellationToken));


    [HttpPost("add-stock-details")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StockDetails))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddStockDetails([FromBody] AddStockRequest request, CancellationToken cancellationToken = default)
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

    [HttpPut("update-stock-details")]
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

    [HttpDelete("delete-stock-price/{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteStockPrice(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return BadRequest("Invalid input parameters.");

        if (!await stockPriceRepository.Delete(id, cancellationToken))
            return NotFound();

        return NoContent();
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

    [HttpPost("bulk-import-close-prices")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StockPriceBulkImportResultDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkImportClosePrices([FromForm] IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest("CSV file is required.");

        if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .csv files are supported.");

        await using var stream = file.OpenReadStream();
        StockPriceBulkImportResultDto result;
        try
        {
            result = await stockPriceBulkImportService.ImportClosePrices(stream, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        return Ok(result);
    }
}