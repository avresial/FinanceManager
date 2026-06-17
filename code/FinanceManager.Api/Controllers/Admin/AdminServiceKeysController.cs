using FinanceManager.Application.Shared.ExternalServices;
using FinanceManager.Domain.Shared.ExternalServices.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Admin;

[Route("api/admin/service-keys")]
[Authorize(Roles = "Admin")]
[ApiController]
[Tags("Admin - Service Keys")]
public class AdminServiceKeysController(IExternalServiceConfigService configService) : ControllerBase
{
    private static readonly IReadOnlyList<(string ServiceName, string DisplayName, string Description, string DocsUrl)> _knownServices =
    [
        ("AlphaVantage", "Alpha Vantage", "Stock market data — daily price series, ticker search and listing status.", "https://www.alphavantage.co/documentation/"),
        ("OpenFigi",     "OpenFIGI",      "Maps ticker symbols to ISINs. Optional key raises rate limit from 25 to 250 req/min.", "https://www.openfigi.com/api"),
    ];

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ExternalServicesResponse))]
    public async Task<IActionResult> GetConfiguration(CancellationToken ct)
    {
        var dtos = new List<ExternalServiceDto>(_knownServices.Count);
        foreach (var (serviceName, displayName, description, docsUrl) in _knownServices)
        {
            var config = await configService.GetServiceAsync(serviceName, ct);
            dtos.Add(new ExternalServiceDto(
                serviceName,
                displayName,
                description,
                docsUrl,
                config.BaseUrl,
                !string.IsNullOrEmpty(config.ApiKey),
                config.IsEnabled));
        }

        return Ok(new ExternalServicesResponse(dtos));
    }

    [HttpPut("{serviceName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateService(string serviceName, [FromBody] UpdateServiceKeyRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            return BadRequest("Service name is required.");

        var knownService = _knownServices.FirstOrDefault(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
        if (knownService == default)
            return BadRequest($"Unknown service '{serviceName}'. Supported: {string.Join(", ", _knownServices.Select(s => s.ServiceName))}.");

        var canonicalName = knownService.ServiceName;

        if (string.IsNullOrWhiteSpace(request.BaseUrl))
            return BadRequest("BaseUrl is required.");

        string apiKey;
        if (request.ApiKey is null)
        {
            var existing = await configService.GetServiceAsync(canonicalName, ct);
            apiKey = existing.ApiKey;
        }
        else
        {
            apiKey = request.ApiKey;
        }

        var config = new ExternalServiceConfiguration
        {
            ServiceName = canonicalName,
            BaseUrl = request.BaseUrl,
            ApiKey = apiKey,
            IsEnabled = request.IsEnabled,
        };

        await configService.SaveServiceAsync(config, ct);
        return NoContent();
    }
}