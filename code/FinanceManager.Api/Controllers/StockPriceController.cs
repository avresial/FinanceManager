using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;
[Route("api/[controller]")]
[ApiController]
public class StockPriceController(IStockPriceRepository stockRepository, ICurrencyExchangeService currencyExchangeService) : ControllerBase
{
    private readonly IStockPriceRepository _stockPriceRepository = stockRepository;
    private readonly ICurrencyExchangeService _currencyExchangeService = currencyExchangeService;

    [Authorize]
    [HttpPost("add-stock-price")]
    public async Task<IActionResult> AddStockPrice([FromQuery] string ticker, [FromQuery] decimal pricePerUnit, [FromQuery] string currency, [FromQuery] DateTime date)
    {
        if (string.IsNullOrWhiteSpace(ticker) || pricePerUnit <= 0 || string.IsNullOrWhiteSpace(currency) || date == default)
            return BadRequest("Invalid input parameters.");

        var stockPrice = await _stockPriceRepository.AddStockPrice(ticker.ToUpper(), pricePerUnit, currency.ToUpper(), date);
        return Ok(stockPrice);
    }

    [Authorize]
    [HttpPost("update-stock-price")]
    public async Task<IActionResult> UpdateStockPrice([FromQuery] string ticker, [FromQuery] decimal pricePerUnit, [FromQuery] string currency, [FromQuery] DateTime date)
    {
        if (string.IsNullOrWhiteSpace(ticker) || pricePerUnit <= 0 || string.IsNullOrWhiteSpace(currency) || date == default)
            return BadRequest("Invalid input parameters.");

        var stockPrice = await _stockPriceRepository.UpdateStockPrice(ticker.ToUpper(), pricePerUnit, currency.ToUpper(), date);
        return Ok(stockPrice);
    }

    [HttpGet("get-stock-price")]
    public async Task<IActionResult> GetStockPrice(string ticker, string currency, DateTime date)
    {
        if (string.IsNullOrWhiteSpace(ticker) || date == default)
            return BadRequest("Invalid input parameters.");

        var stockPrice = await _stockPriceRepository.GetStockPrice(ticker, date);

        if (stockPrice is null) return NotFound("Stock price not found.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Equals(stockPrice.Currency, StringComparison.OrdinalIgnoreCase))
            return Ok(stockPrice);

        var exchangeRate = await _currencyExchangeService.GetExchangeRateAsync(stockPrice.Currency, currency, date); // TODO add cache
        if (exchangeRate is null)
            return NotFound($"Exchange rate from {stockPrice.Currency} to {currency} not found for the specified date.");

        stockPrice.PricePerUnit = stockPrice.PricePerUnit * exchangeRate.Value;
        stockPrice.Currency = currency.ToUpper();

        return Ok(stockPrice);
    }


    [HttpGet("get-stock-prices")]
    public async Task<IActionResult> GetStockPrices(string ticker, DateTime start, DateTime end, long step)
    {
        if (string.IsNullOrWhiteSpace(ticker) || start == default || end == default || step == default)
            return BadRequest("Invalid input parameters.");

        List<StockPrice> stockPrices = [];

        for (var i = start; i < end; i = i.Add(new(step)))
        {
            var stockPrice = await _stockPriceRepository.GetStockPrice(ticker, i);
            if (stockPrice is null) continue;
            stockPrices.Add(stockPrice);
        }


        if (!stockPrices.Any()) return NotFound("Stock prices not found.");

        return Ok(stockPrices);
    }

    [HttpGet("get-latest-missing-stock-price")]
    public async Task<IActionResult> GetLatestMissingStockPrice(string ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            return BadRequest("Invalid input parameters.");

        var stockPrice = await _stockPriceRepository.GetLatestMissingStockPrice(ticker);

        if (stockPrice is null) return NotFound("Stock price not found.");

        return Ok(stockPrice);
    }

    [HttpGet("get-ticker-currency")]
    public async Task<IActionResult> GetTickerCurrency(string ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            return BadRequest("Invalid input parameters.");

        var currency = await _stockPriceRepository.GetTickerCurrency(ticker);

        if (currency is null) return NotFound("Stock price not found.");

        return Ok(new TickerCurrency(ticker, currency));
    }
}
