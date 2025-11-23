using FinanceManager.Domain.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

/// <summary>
/// Example controller demonstrating how to use the IInflationDataProvider.
/// This can be removed once integrated into your actual services.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class InflationController(IInflationDataProvider inflationDataProvider) : ControllerBase
{

    /// <summary>
    /// Get inflation rate for a specific currency and date.
    /// </summary>
    /// <param name="currencyId">Currency ID (e.g., 1 for PLN)</param>
    /// <param name="date">Date in YYYY-MM-DD format</param>
    /// <returns>Inflation rate as a percentage</returns>
    [HttpGet("{currencyId}/{date}")]
    public async Task<ActionResult<decimal>> GetInflationRate(int currencyId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var rate = await inflationDataProvider.GetInflationRateAsync(currencyId, date, cancellationToken);

        if (rate is null)
            return NotFound($"No inflation data found for currency {currencyId} on {date}");

        return Ok(rate);
    }

    /// <summary>
    /// Get inflation rates for a specific currency within a date range.
    /// </summary>
    /// <param name="currencyId">Currency ID (e.g., 1 for PLN)</param>
    /// <param name="from">Start date in YYYY-MM-DD format</param>
    /// <param name="to">End date in YYYY-MM-DD format</param>
    /// <returns>List of inflation rates</returns>
    [HttpGet("{currencyId}/range")]
    public async Task<ActionResult> GetInflationRates(
        int currencyId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var rates = await inflationDataProvider.GetInflationRatesAsync(currencyId, from, to, cancellationToken);
        return Ok(rates);
    }
}