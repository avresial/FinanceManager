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
    public async Task<IActionResult> AddStockPrice(string ticker, decimal pricePerUnit, string currency, DateTime date)
    {
        if (string.IsNullOrWhiteSpace(ticker) || pricePerUnit <= 0 || string.IsNullOrWhiteSpace(currency) || date == default)
            return BadRequest("Invalid input parameters.");

        var stockPrice = await _stockRepository.AddStockPrice(ticker, pricePerUnit, currency, date);
        return Ok(stockPrice);
    }

    [HttpGet("get-stock-price")]
    public async Task<IActionResult> GetStockPrice(string ticker, DateTime date)
    {
        if (string.IsNullOrWhiteSpace(ticker) || date == default)
            return BadRequest("Invalid input parameters.");

        var stockPrice = await _stockRepository.GetStockPrice(ticker, date);

        if (stockPrice is null)
            return NotFound("Stock price not found.");

        return Ok(stockPrice);
    }
}