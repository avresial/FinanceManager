using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;
[Route("api/[controller]")]
[ApiController]
public class StockPriceController(IStockRepository stockRepository) : ControllerBase
{
    private readonly IStockRepository _stockRepository = stockRepository;

    [Authorize]
    [HttpPost("add-stock-price")]
    public async Task<IActionResult> AddStockPrice([FromQuery] string ticker, [FromQuery] decimal pricePerUnit, [FromQuery] string currency, [FromQuery] DateTime date)
    {
        if (string.IsNullOrWhiteSpace(ticker) || pricePerUnit <= 0 || string.IsNullOrWhiteSpace(currency) || date == default)
            return BadRequest("Invalid input parameters.");

        var stockPrice = await _stockRepository.AddStockPrice(ticker.ToUpper(), pricePerUnit, currency.ToUpper(), date);
        return Ok(stockPrice);
    }

    [Authorize]
    [HttpPost("update-stock-price")]
    public async Task<IActionResult> UpdateStockPrice([FromQuery] string ticker, [FromQuery] decimal pricePerUnit, [FromQuery] string currency, [FromQuery] DateTime date)
    {
        return NotFound();
        //if (string.IsNullOrWhiteSpace(ticker) || pricePerUnit <= 0 || string.IsNullOrWhiteSpace(currency) || date == default)
        //    return BadRequest("Invalid input parameters.");

        //var stockPrice = await _stockRepository.AddStockPrice(ticker, pricePerUnit, currency, date);
        //return Ok(stockPrice);
    }

    [HttpGet("get-stock-price")]
    public async Task<IActionResult> GetStockPrice(string ticker, DateTime date)
    {
        if (string.IsNullOrWhiteSpace(ticker) || date == default)
            return BadRequest("Invalid input parameters.");

        var stockPrice = await _stockRepository.GetStockPrice(ticker, DefaultCurrency.Currency, date);

        if (stockPrice is null)
            return NotFound("Stock price not found.");

        return Ok(stockPrice);
    }


    [HttpGet("get-stock-prices")]
    public async Task<IActionResult> GetStockPrices(string ticker, DateTime start, DateTime end, long step)
    {
        if (string.IsNullOrWhiteSpace(ticker) || start == default || end == default || step == default)
            return BadRequest("Invalid input parameters.");

        List<StockPrice> stockPrices = new List<StockPrice>();


        for (var i = start; i < end; i = i.Add(new(step)))
        {
            var stockPrice = await _stockRepository.GetStockPrice(ticker, DefaultCurrency.Currency, i);
            if (stockPrice is null) continue;
            stockPrices.Add(stockPrice);
        }


        if (!stockPrices.Any())
            return NotFound("Stock prices not found.");

        return Ok(stockPrices);
    }

    [HttpGet("get-latest-missing-stock-price")]
    public async Task<IActionResult> GetLatestMissingStockPrice(string ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            return BadRequest("Invalid input parameters.");

        var stockPrice = await _stockRepository.GetLatestMissingStockPrice(ticker);

        if (stockPrice is null) return NotFound("Stock price not found.");

        return Ok(stockPrice);

    }

}