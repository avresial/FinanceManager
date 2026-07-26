using FinanceManager.Components.Features.Administration.HttpClients;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Features.Administration.Components;

public partial class AdminAiProviders : ComponentBase
{
    private sealed class ProviderModel
    {
        public string ProviderName { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public bool HasApiKey { get; set; }
        public string NewApiKey { get; set; } = string.Empty;
        public int RequestTimeoutSeconds { get; set; } = 180;
        public bool IsEnabled { get; set; } = true;
        public List<ModelEntry> Models { get; set; } = [];
        public string NewModelName { get; set; } = string.Empty;

        private string _savedBaseUrl = string.Empty;
        private int _savedTimeout = 180;
        private bool _savedIsEnabled = true;

        public bool IsDirty =>
            BaseUrl != _savedBaseUrl
            || RequestTimeoutSeconds != _savedTimeout
            || IsEnabled != _savedIsEnabled
            || !string.IsNullOrEmpty(NewApiKey);

        public void CaptureSnapshot()
        {
            _savedBaseUrl = BaseUrl;
            _savedTimeout = RequestTimeoutSeconds;
            _savedIsEnabled = IsEnabled;
        }
    }

    private sealed class ModelEntry
    {
        public int Id { get; set; }
        public string ModelName { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
    }

    private sealed class FallbackEntryModel
    {
        public string ProviderName { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
    }

    [Inject] public AdminAiProvidersHttpClient ApiClient { get; set; } = default!;
    [Inject] public ISnackbar Snackbar { get; set; } = default!;

    private bool _isLoading = true;
    private bool _savingFallback;
    private readonly HashSet<string> _savingProviders = [];
    private readonly HashSet<string> _addingModel = [];
    private readonly HashSet<int> _togglingModel = [];
    private readonly HashSet<int> _deletingModel = [];
    private readonly HashSet<string> _deletingProvider = [];
    private readonly List<string> _errors = [];
    private readonly List<ProviderModel> _providers = [];
    private readonly List<FallbackEntryModel> _fallbackEntries = [];
    private readonly Dictionary<string, bool> _showApiKey = [];
    private List<string> _availableToAdd = [];
    private List<string> _knownProviders = [];
    private bool _addingProvider;
    private string _savedFallbackSignature = string.Empty;

    private bool FallbackDirty => FallbackSignature() != _savedFallbackSignature;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _errors.Clear();
        try
        {
            var config = await ApiClient.GetConfigurationAsync();
            if (config is null) return;

            _providers.Clear();
            foreach (var p in config.Providers)
            {
                _providers.Add(ToProviderModel(p));
                _showApiKey[p.ProviderName] = false;
            }

            _fallbackEntries.Clear();
            foreach (var e in config.FallbackEntries.OrderBy(x => x.Order))
            {
                _fallbackEntries.Add(new FallbackEntryModel
                {
                    ProviderName = e.ProviderName,
                    Model = e.Model,
                });
            }
            _savedFallbackSignature = FallbackSignature();

            _knownProviders = config.KnownProviders.ToList();
            RecomputeAvailableToAdd();
        }
        catch (Exception ex)
        {
            _errors.Add($"Failed to load configuration: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    // Fetches the latest config and appends any provider card not already shown, leaving existing cards
    // (and their unsaved edits / dirty state) untouched.
    private async Task AddProviderCardAsync(string providerName)
    {
        var config = await ApiClient.GetConfigurationAsync();
        if (config is null) return;

        _knownProviders = config.KnownProviders.ToList();
        var existing = _providers.Select(p => p.ProviderName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var p in config.Providers.Where(p => !existing.Contains(p.ProviderName)))
        {
            _providers.Add(ToProviderModel(p));
            _showApiKey[p.ProviderName] = false;
        }
        RecomputeAvailableToAdd();
    }

    // Replaces just one provider's model list from the server (e.g. after adding a model) without
    // rebuilding any card, so dirty edits everywhere survive.
    private async Task RefreshProviderModelsAsync(ProviderModel provider)
    {
        var config = await ApiClient.GetConfigurationAsync();
        var dto = config?.Providers.FirstOrDefault(p => p.ProviderName.Equals(provider.ProviderName, StringComparison.OrdinalIgnoreCase));
        if (dto is null) return;
        provider.Models = dto.Models.Select(ToModelEntry).ToList();
    }

    private void RecomputeAvailableToAdd()
    {
        var configuredNames = _providers.Select(p => p.ProviderName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _availableToAdd = _knownProviders.Where(n => !configuredNames.Contains(n)).ToList();
    }

    private static ProviderModel ToProviderModel(AiProviderDto p)
    {
        var provider = new ProviderModel
        {
            ProviderName = p.ProviderName,
            BaseUrl = p.BaseUrl,
            HasApiKey = p.HasApiKey,
            RequestTimeoutSeconds = p.RequestTimeoutSeconds,
            IsEnabled = p.IsEnabled,
            Models = p.Models.Select(ToModelEntry).ToList(),
        };
        provider.CaptureSnapshot();
        return provider;
    }

    private static ModelEntry ToModelEntry(AiProviderModelDto m) => new()
    {
        Id = m.Id,
        ModelName = m.ModelName,
        IsEnabled = m.IsEnabled,
    };

    private async Task AddProviderAsync(string providerName)
    {
        if (string.IsNullOrEmpty(providerName)) return;
        _addingProvider = true;
        try
        {
            await ApiClient.AddProviderAsync(new AddProviderRequest(providerName));
            // Add only the new card so unsaved edits on existing cards (and their dirty state) survive.
            await AddProviderCardAsync(providerName);
            Snackbar.Add($"{providerName} added.", Severity.Success);
        }
        catch (Exception ex)
        {
            _errors.Add($"Failed to add provider: {ex.Message}");
        }
        finally
        {
            _addingProvider = false;
        }
    }

    private async Task DeleteProviderAsync(ProviderModel provider)
    {
        _deletingProvider.Add(provider.ProviderName);
        try
        {
            await ApiClient.DeleteProviderAsync(provider.ProviderName);
            // Drop only this card locally so unsaved edits on the remaining cards survive.
            _providers.Remove(provider);
            _showApiKey.Remove(provider.ProviderName);
            RecomputeAvailableToAdd();
            Snackbar.Add($"{provider.ProviderName} removed.", Severity.Success);
        }
        catch (Exception ex)
        {
            _errors.Add($"Failed to remove {provider.ProviderName}: {ex.Message}");
        }
        finally
        {
            _deletingProvider.Remove(provider.ProviderName);
        }
    }

    private async Task SaveProviderAsync(ProviderModel provider)
    {
        _savingProviders.Add(provider.ProviderName);
        try
        {
            string? apiKey = string.IsNullOrEmpty(provider.NewApiKey) ? null : provider.NewApiKey;

            await ApiClient.UpdateProviderAsync(provider.ProviderName, new UpdateProviderRequest(
                provider.BaseUrl,
                apiKey,
                provider.RequestTimeoutSeconds,
                provider.IsEnabled));

            provider.HasApiKey = provider.HasApiKey || !string.IsNullOrEmpty(provider.NewApiKey);
            provider.NewApiKey = string.Empty;
            provider.CaptureSnapshot();
            Snackbar.Add($"{provider.ProviderName} saved.", Severity.Success);
        }
        catch (Exception ex)
        {
            _errors.Add($"Failed to save {provider.ProviderName}: {ex.Message}");
        }
        finally
        {
            _savingProviders.Remove(provider.ProviderName);
        }
    }

    private async Task AddModelAsync(ProviderModel provider)
    {
        if (string.IsNullOrWhiteSpace(provider.NewModelName)) return;
        _addingModel.Add(provider.ProviderName);
        try
        {
            await ApiClient.AddModelAsync(provider.ProviderName, new AddModelRequest(provider.NewModelName.Trim()));
            provider.NewModelName = string.Empty;
            // Re-pull just this provider's models (to get the new model's id) without rebuilding every
            // card, so unsaved edits and dirty state on other cards — and this one — are preserved.
            await RefreshProviderModelsAsync(provider);
            Snackbar.Add("Model added.", Severity.Success);
        }
        catch (Exception ex)
        {
            _errors.Add($"Failed to add model: {ex.Message}");
        }
        finally
        {
            _addingModel.Remove(provider.ProviderName);
        }
    }

    private async Task ToggleModelAsync(ProviderModel provider, ModelEntry model)
    {
        _togglingModel.Add(model.Id);
        try
        {
            model.IsEnabled = !model.IsEnabled;
            await ApiClient.UpdateModelAsync(provider.ProviderName, model.Id,
                new UpdateModelRequest(model.ModelName, model.IsEnabled));
        }
        catch (Exception ex)
        {
            model.IsEnabled = !model.IsEnabled;
            _errors.Add($"Failed to update model: {ex.Message}");
        }
        finally
        {
            _togglingModel.Remove(model.Id);
        }
    }

    private async Task DeleteModelAsync(ProviderModel provider, ModelEntry model)
    {
        _deletingModel.Add(model.Id);
        try
        {
            await ApiClient.DeleteModelAsync(provider.ProviderName, model.Id);
            provider.Models.Remove(model);
            Snackbar.Add("Model removed.", Severity.Success);
        }
        catch (Exception ex)
        {
            _errors.Add($"Failed to remove model: {ex.Message}");
        }
        finally
        {
            _deletingModel.Remove(model.Id);
        }
    }

    private async Task SaveFallbackAsync()
    {
        _savingFallback = true;
        try
        {
            var entries = _fallbackEntries
                .Select((e, i) => new AiFallbackEntryDto(e.ProviderName, e.Model, i))
                .ToList();

            await ApiClient.UpdateFallbackAsync(new UpdateFallbackRequest(entries));
            _savedFallbackSignature = FallbackSignature();
            Snackbar.Add("Fallback strategy saved.", Severity.Success);
        }
        catch (Exception ex)
        {
            _errors.Add($"Failed to save fallback strategy: {ex.Message}");
        }
        finally
        {
            _savingFallback = false;
        }
    }

    private void AddFallbackEntry()
    {
        var provider = _providers.FirstOrDefault();
        _fallbackEntries.Add(new FallbackEntryModel
        {
            ProviderName = provider?.ProviderName ?? string.Empty,
            Model = provider?.Models.FirstOrDefault()?.ModelName ?? string.Empty,
        });
    }

    private void MoveFallbackEntry(FallbackEntryModel entry, int direction)
    {
        var index = _fallbackEntries.IndexOf(entry);
        var newIndex = index + direction;
        if (newIndex < 0 || newIndex >= _fallbackEntries.Count) return;
        _fallbackEntries.RemoveAt(index);
        _fallbackEntries.Insert(newIndex, entry);
    }

    private void OnFallbackProviderChanged(FallbackEntryModel entry, string providerName)
    {
        entry.ProviderName = providerName;
        var models = ModelsForProvider(providerName).ToList();
        if (!models.Any(m => m.ModelName.Equals(entry.Model, StringComparison.OrdinalIgnoreCase)))
            entry.Model = models.FirstOrDefault()?.ModelName ?? string.Empty;
    }

    private IEnumerable<ModelEntry> ModelsForProvider(string providerName) =>
        _providers.FirstOrDefault(p => p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase))?.Models
        ?? Enumerable.Empty<ModelEntry>();

    private string FallbackSignature() =>
        string.Join("|", _fallbackEntries.Select(e => $"{e.ProviderName}>{e.Model}"));

    private void ToggleApiKeyVisibility(string providerName)
    {
        _showApiKey[providerName] = !_showApiKey[providerName];
    }

    private bool IsProviderDisabledInFallback(string providerName) =>
        _providers.FirstOrDefault(p => p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase)) is { IsEnabled: false };

    private bool IsModelDisabledInFallback(string providerName, string modelName)
    {
        var provider = _providers.FirstOrDefault(p => p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));
        if (provider is null) return false;
        var model = provider.Models.FirstOrDefault(m => m.ModelName.Equals(modelName, StringComparison.OrdinalIgnoreCase));
        return model is { IsEnabled: false };
    }
}