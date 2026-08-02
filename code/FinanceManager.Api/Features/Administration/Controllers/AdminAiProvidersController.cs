using FinanceManager.Application.Shared.Ai;
using FinanceManager.Domain.Administration;
using FinanceManager.Domain.Shared.Ai.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Features.Administration.Controllers;

[Route("api/admin/ai-providers")]
[Authorize(Roles = "Admin")]
[ApiController]
[Tags("Admin - AI Providers")]
public class AdminAiProvidersController(IAiConfigurationService configService) : ControllerBase
{
    // The catalogue of providers the app can talk to. Every entry is always listed, with its stored
    // configuration falling back to appsettings — the same contract as Admin - Service Keys, so a key
    // supplied through configuration is visible without an admin registering the provider first.
    private static readonly IReadOnlyList<(string ProviderName, string DisplayName, string Description, string DocsUrl)> _knownProviders =
    [
        ("OpenRouter", "OpenRouter", "Hosted gateway to many commercial and open models through one OpenAI-compatible key.", "https://openrouter.ai/docs"),
        ("GitHub",     "GitHub Models", "Models hosted by GitHub, authenticated with a personal access token.", "https://docs.github.com/en/github-models"),
        ("Ollama",     "Ollama",     "Local model runner. Needs no API key — only a reachable base URL.", "https://github.com/ollama/ollama/blob/main/docs/api.md"),
        ("LmStudio",   "LM Studio",  "Local OpenAI-compatible server for models running on your own machine.", "https://lmstudio.ai/docs/app/api"),
    ];

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AiConfigurationResponse))]
    public async Task<IActionResult> GetConfiguration(CancellationToken ct)
    {
        var fallback = await configService.GetFallbackEntriesAsync(ct);
        var models = await configService.GetAllModelsAsync(ct);

        var modelsByProvider = models
            .GroupBy(m => m.ProviderName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(m => new AiProviderModelDto(m.Id, m.ModelName, m.IsEnabled)).ToList(), StringComparer.OrdinalIgnoreCase);

        var providerDtos = new List<AiProviderDto>(_knownProviders.Count);
        foreach (var (providerName, displayName, description, docsUrl) in _knownProviders)
        {
            var config = await configService.GetProviderAsync(providerName, ct);
            providerDtos.Add(new AiProviderDto(
                providerName,
                displayName,
                description,
                docsUrl,
                config.BaseUrl,
                !string.IsNullOrEmpty(config.ApiKey),
                config.RequestTimeoutSeconds,
                config.IsEnabled,
                modelsByProvider.TryGetValue(providerName, out var providerModels) ? providerModels : []));
        }

        var fallbackDtos = fallback.Select(e => new AiFallbackEntryDto(e.ProviderName, e.Model, e.Order)).ToList();

        return Ok(new AiConfigurationResponse(providerDtos, fallbackDtos));
    }

    [HttpPut("{providerName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProvider(string providerName, [FromBody] UpdateProviderRequest request, CancellationToken ct)
    {
        if (ResolveProviderName(providerName) is not string canonicalName)
            return BadRequest(UnknownProviderMessage(providerName));

        if (string.IsNullOrWhiteSpace(request.BaseUrl))
            return BadRequest("BaseUrl is required.");

        string apiKey;
        if (request.ApiKey is null)
        {
            var existing = await configService.GetProviderAsync(canonicalName, ct);
            apiKey = existing.ApiKey;
        }
        else
        {
            apiKey = request.ApiKey;
        }

        var config = new AiProviderConfiguration
        {
            ProviderName = canonicalName,
            BaseUrl = request.BaseUrl,
            ApiKey = apiKey,
            RequestTimeoutSeconds = request.RequestTimeoutSeconds,
            IsEnabled = request.IsEnabled,
        };

        await configService.SaveProviderAsync(config, ct);
        return NoContent();
    }

    [HttpPost("{providerName}/models")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddModel(string providerName, [FromBody] AddModelRequest request, CancellationToken ct)
    {
        if (ResolveProviderName(providerName) is not string canonicalName)
            return BadRequest(UnknownProviderMessage(providerName));

        if (string.IsNullOrWhiteSpace(request.ModelName))
            return BadRequest("Model name is required.");

        await configService.AddModelAsync(new AiProviderModel
        {
            ProviderName = canonicalName,
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
        if (ResolveProviderName(providerName) is not string canonicalName)
            return BadRequest(UnknownProviderMessage(providerName));

        if (string.IsNullOrWhiteSpace(request.ModelName))
            return BadRequest("Model name is required.");

        await configService.UpdateModelAsync(new AiProviderModel
        {
            Id = modelId,
            ProviderName = canonicalName,
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

    // Providers are keyed by name, so route values are mapped back onto the catalogue spelling before
    // they reach storage — otherwise "openrouter" would create a second row alongside "OpenRouter".
    private static string? ResolveProviderName(string providerName) =>
        _knownProviders
            .FirstOrDefault(p => p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase))
            .ProviderName;

    private static string UnknownProviderMessage(string providerName) =>
        $"Unknown provider '{providerName}'. Supported: {string.Join(", ", _knownProviders.Select(p => p.ProviderName))}.";
}