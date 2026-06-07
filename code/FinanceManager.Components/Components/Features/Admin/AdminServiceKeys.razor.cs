using FinanceManager.Components.HttpClients;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.Admin;

public partial class AdminServiceKeys : ComponentBase
{
    private sealed class ServiceModel
    {
        public string ServiceName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DocsUrl { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public bool HasApiKey { get; set; }
        public string NewApiKey { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;

        private string _savedBaseUrl = string.Empty;
        private bool _savedIsEnabled = true;

        public bool IsDirty =>
            BaseUrl != _savedBaseUrl
            || IsEnabled != _savedIsEnabled
            || !string.IsNullOrWhiteSpace(NewApiKey);

        public void CaptureSnapshot()
        {
            _savedBaseUrl = BaseUrl;
            _savedIsEnabled = IsEnabled;
        }
    }

    [Inject] public AdminServiceKeysHttpClient ApiClient { get; set; } = default!;
    [Inject] public ISnackbar Snackbar { get; set; } = default!;

    private bool _isLoading = true;
    private readonly List<string> _errors = [];
    private readonly List<ServiceModel> _services = [];
    private readonly HashSet<string> _savingServices = [];
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
            var response = await ApiClient.GetConfigurationAsync();
            if (response is null) return;

            _services.Clear();
            _showApiKey.Clear();
            foreach (var s in response.Services)
            {
                _services.Add(ToModel(s));
                _showApiKey[s.ServiceName] = false;
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

    private static ServiceModel ToModel(ExternalServiceDto dto)
    {
        var model = new ServiceModel
        {
            ServiceName = dto.ServiceName,
            DisplayName = dto.DisplayName,
            Description = dto.Description,
            DocsUrl = dto.DocsUrl,
            BaseUrl = dto.BaseUrl,
            HasApiKey = dto.HasApiKey,
            IsEnabled = dto.IsEnabled,
        };
        model.CaptureSnapshot();
        return model;
    }

    private async Task SaveServiceAsync(ServiceModel service)
    {
        _savingServices.Add(service.ServiceName);
        try
        {
            string? apiKey = string.IsNullOrWhiteSpace(service.NewApiKey) ? null : service.NewApiKey.Trim();

            await ApiClient.UpdateServiceAsync(service.ServiceName, new UpdateServiceKeyRequest(
                service.BaseUrl,
                apiKey,
                service.IsEnabled));

            service.HasApiKey = service.HasApiKey || !string.IsNullOrWhiteSpace(service.NewApiKey);
            service.NewApiKey = string.Empty;
            service.CaptureSnapshot();
            Snackbar.Add($"{service.DisplayName} saved.", Severity.Success);
        }
        catch (Exception ex)
        {
            _errors.Add($"Failed to save {service.DisplayName}: {ex.Message}");
        }
        finally
        {
            _savingServices.Remove(service.ServiceName);
        }
    }

    private void ToggleApiKeyVisibility(string serviceName)
    {
        _showApiKey[serviceName] = !_showApiKey.GetValueOrDefault(serviceName);
    }
}