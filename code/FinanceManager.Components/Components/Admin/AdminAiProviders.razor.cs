using FinanceManager.Components.HttpClients;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.Admin;

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
    private string? _selectedProviderToAdd;
    private bool _addingProvider;

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
                _providers.Add(new ProviderModel
                {
                    ProviderName = p.ProviderName,
                    BaseUrl = p.BaseUrl,
                    HasApiKey = p.HasApiKey,
                    RequestTimeoutSeconds = p.RequestTimeoutSeconds,
                    IsEnabled = p.IsEnabled,
                    Models = p.Models.Select(m => new ModelEntry
                    {
                        Id = m.Id,
                        ModelName = m.ModelName,
                        IsEnabled = m.IsEnabled,
                    }).ToList(),
                });
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

            var configuredNames = _providers.Select(p => p.ProviderName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            _availableToAdd = config.KnownProviders.Where(n => !configuredNames.Contains(n)).ToList();
            _selectedProviderToAdd = _availableToAdd.FirstOrDefault();
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

    private async Task AddProviderAsync()
    {
        if (string.IsNullOrEmpty(_selectedProviderToAdd)) return;
        _addingProvider = true;
        try
        {
            await ApiClient.AddProviderAsync(new AddProviderRequest(_selectedProviderToAdd));
            await LoadAsync();
            Snackbar.Add($"{_selectedProviderToAdd} added.", Severity.Success);
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
            await LoadAsync();
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
            await LoadAsync();
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
        _fallbackEntries.Add(new FallbackEntryModel
        {
            ProviderName = _providers.FirstOrDefault()?.ProviderName ?? string.Empty,
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
