using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace FinanceManager.Components.Components.Admin;

public partial class LabelSetterProgressCard : ComponentBase, IAsyncDisposable
{
    private LabelSetterProgressSnapshot? _snapshot;
    private string? _loadError;
    private HubConnection? _hubConnection;

    [Inject] public required LabelSetterProgressHttpClient ProgressHttpClient { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _snapshot = await ProgressHttpClient.GetSnapshot();
        }
        catch (Exception ex)
        {
            _loadError = $"Failed to load progress: {ex.Message}";
        }

        await ConnectHub();
    }

    private async Task ConnectHub()
    {
        try
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(NavigationManager.ToAbsoluteUri("hubs/label-setter-progress"), options =>
                {
                    options.AccessTokenProvider = async () => (await LoginService.GetLoggedUser())?.Token;
                })
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<LabelSetterProgressSnapshot>("ProgressUpdated", snapshot =>
            {
                _snapshot = snapshot;
                _ = InvokeAsync(StateHasChanged);
            });

            _hubConnection.Reconnected += async _ =>
            {
                await _hubConnection.InvokeAsync("Subscribe");
            };

            await _hubConnection.StartAsync();
            await _hubConnection.InvokeAsync("Subscribe");
        }
        catch (Exception ex)
        {
            _loadError = $"Live updates disabled: {ex.Message}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }
}