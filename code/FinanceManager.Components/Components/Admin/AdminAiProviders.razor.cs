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
    }

    private sealed class FallbackEntryModel
    {
        public string ProviderName { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
    }

    [Inject] public AdminAiProvidersHttpClient ApiClient { get; set; } = default!;
    [Inject] public ISnackbar Snackbar { get; set; } = default!;

    private static readonly string[] _knownProviders = ["OpenRouter", "LmStudio", "Ollama", "GitHub"];

    private bool _isLoading = true;
    private bool _savingFallback;
    private readonly HashSet<string> _savingProviders = [];
    private readonly List<string> _errors = [];
    private readonly List<ProviderModel> _providers = [];
    private readonly List<FallbackEntryModel> _fallbackEntries = [];
    private readonly Dictionary<string, bool> _showApiKey = [];

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

    private async Task SaveProviderAsync(ProviderModel provider)
    {
        _savingProviders.Add(provider.ProviderName);
        try
        {
            // Send null ApiKey if the user left the field empty (preserve existing key)
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
            ProviderName = _knownProviders[0],
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
}
