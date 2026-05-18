using FinanceManager.Application.Services.Ai;
using FinanceManager.Domain.Entities.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Admin;

[Route("api/admin/ai-providers")]
[Authorize(Roles = "Admin")]
[ApiController]
[Tags("Admin - AI Providers")]
public class AdminAiProvidersController(IAiConfigurationService configService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AiConfigurationResponse))]
    public async Task<IActionResult> GetConfiguration(CancellationToken ct)
    {
        var providers = await configService.GetAllProvidersAsync(ct);
        var fallback = await configService.GetFallbackEntriesAsync(ct);

        var providerDtos = providers.Select(p => new AiProviderDto(
            p.ProviderName,
            p.BaseUrl,
            !string.IsNullOrEmpty(p.ApiKey),
            p.RequestTimeoutSeconds,
            p.IsEnabled)).ToList();

        var fallbackDtos = fallback.Select(e => new AiFallbackEntryDto(e.ProviderName, e.Model, e.Order)).ToList();

        return Ok(new AiConfigurationResponse(providerDtos, fallbackDtos));
    }

    [HttpPut("{providerName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProvider(string providerName, [FromBody] UpdateProviderRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            return BadRequest("Provider name is required.");

        if (string.IsNullOrWhiteSpace(request.BaseUrl))
            return BadRequest("BaseUrl is required.");

        string apiKey;
        if (request.ApiKey is null)
        {
            var existing = await configService.GetProviderAsync(providerName, ct);
            apiKey = existing.ApiKey;
        }
        else
        {
            apiKey = request.ApiKey;
        }

        var config = new AiProviderConfiguration
        {
            ProviderName = providerName,
            BaseUrl = request.BaseUrl,
            ApiKey = apiKey,
            RequestTimeoutSeconds = request.RequestTimeoutSeconds,
            IsEnabled = request.IsEnabled,
        };

        await configService.SaveProviderAsync(config, ct);
        return NoContent();
    }

    [HttpPut("fallback")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateFallbackStrategy([FromBody] UpdateFallbackRequest request, CancellationToken ct)
    {
        var entries = request.Entries
            .Select((e, i) => new AiFallbackEntry
            {
                ProviderName = e.ProviderName,
                Model = e.Model,
                Order = i,
            })
            .ToList();

        await configService.SaveFallbackEntriesAsync(entries, ct);
        return NoContent();
    }
}
