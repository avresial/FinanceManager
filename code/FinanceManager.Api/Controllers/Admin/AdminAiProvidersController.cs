using FinanceManager.Application.Shared.Ai;
using FinanceManager.Domain.Administration;
using FinanceManager.Domain.Shared.Ai.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers.Admin;

[Route("api/admin/ai-providers")]
[Authorize(Roles = "Admin")]
[ApiController]
[Tags("Admin - AI Providers")]
public class AdminAiProvidersController(IAiConfigurationService configService) : ControllerBase
{
    private static readonly string[] _knownProviders = ["OpenRouter", "LmStudio", "Ollama", "GitHub"];

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AiConfigurationResponse))]
    public async Task<IActionResult> GetConfiguration(CancellationToken ct)
    {
        var providers = await configService.GetAllProvidersAsync(ct);
        var fallback = await configService.GetFallbackEntriesAsync(ct);
        var models = await configService.GetAllModelsAsync(ct);

        var modelsByProvider = models
            .GroupBy(m => m.ProviderName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(m => new AiProviderModelDto(m.Id, m.ModelName, m.IsEnabled)).ToList(), StringComparer.OrdinalIgnoreCase);

        var providerDtos = providers.Select(p => new AiProviderDto(
            p.ProviderName,
            p.BaseUrl,
            !string.IsNullOrEmpty(p.ApiKey),
            p.RequestTimeoutSeconds,
            p.IsEnabled,
            modelsByProvider.TryGetValue(p.ProviderName, out var providerModels) ? providerModels : [])).ToList();

        var fallbackDtos = fallback.Select(e => new AiFallbackEntryDto(e.ProviderName, e.Model, e.Order)).ToList();

        return Ok(new AiConfigurationResponse(providerDtos, fallbackDtos, _knownProviders));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddProvider([FromBody] AddProviderRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderName))
            return BadRequest("Provider name is required.");

        if (!_knownProviders.Contains(request.ProviderName, StringComparer.OrdinalIgnoreCase))
            return BadRequest($"Unknown provider '{request.ProviderName}'. Supported: {string.Join(", ", _knownProviders)}.");

        var existing = await configService.GetAllProvidersAsync(ct);
        if (existing.Any(p => p.ProviderName.Equals(request.ProviderName, StringComparison.OrdinalIgnoreCase)))
            return Conflict($"Provider '{request.ProviderName}' is already configured.");

        var defaults = await configService.GetProviderAsync(request.ProviderName, ct);
        var config = new AiProviderConfiguration
        {
            ProviderName = request.ProviderName,
            BaseUrl = defaults.BaseUrl,
            ApiKey = defaults.ApiKey,
            RequestTimeoutSeconds = defaults.RequestTimeoutSeconds,
            IsEnabled = true,
        };
        await configService.SaveProviderAsync(config, ct);
        return NoContent();
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

    [HttpDelete("{providerName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteProvider(string providerName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            return BadRequest("Provider name is required.");

        await configService.DeleteProviderAsync(providerName, ct);
        return NoContent();
    }

    [HttpPost("{providerName}/models")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddModel(string providerName, [FromBody] AddModelRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            return BadRequest("Provider name is required.");

        if (string.IsNullOrWhiteSpace(request.ModelName))
            return BadRequest("Model name is required.");

        await configService.AddModelAsync(new AiProviderModel
        {
            ProviderName = providerName,
            ModelName = request.ModelName.Trim(),
            IsEnabled = true,
        }, ct);
        return NoContent();
    }

    [HttpPut("{providerName}/models/{modelId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateModel(string providerName, int modelId, [FromBody] UpdateModelRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ModelName))
            return BadRequest("Model name is required.");

        await configService.UpdateModelAsync(new AiProviderModel
        {
            Id = modelId,
            ProviderName = providerName,
            ModelName = request.ModelName.Trim(),
            IsEnabled = request.IsEnabled,
        }, ct);
        return NoContent();
    }

    [HttpDelete("{providerName}/models/{modelId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteModel(string providerName, int modelId, CancellationToken ct)
    {
        await configService.DeleteModelAsync(modelId, ct);
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