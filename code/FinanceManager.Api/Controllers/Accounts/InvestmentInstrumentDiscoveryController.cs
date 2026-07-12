using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Domain.Assets.Discovery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Accounts;

/// <summary>
/// User-facing instrument discovery over external providers (OpenFIGI + Alpha Vantage), kept
/// separate from the admin asset CRUD API.
/// </summary>
[Authorize]
[ApiController]
[Route("api/investments/instruments")]
[Tags("Investment Instruments")]
public class InvestmentInstrumentDiscoveryController(IInvestmentInstrumentDiscoveryService discoveryService) : ControllerBase
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
        Ok(await discoveryService.GetImportPreviewAsync(instrument, cancellationToken));

    [HttpPost("import")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ImportedInstrumentDto))]
    public async Task<IActionResult> Import([FromBody] ImportInstrumentCommand command, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await discoveryService.ImportAsync(command, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}