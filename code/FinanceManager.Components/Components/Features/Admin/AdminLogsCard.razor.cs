using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Administration.Logging;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.Admin;

public partial class AdminLogsCard : ComponentBase, IAsyncDisposable
{
    private const int _maxEntries = 5;
    private List<LogEntryDto>? _entries;
    private string? _loadError;
    private HubConnection? _hubConnection;

    [Inject] public required AdminLogsHttpClient AdminLogsHttpClient { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _entries = await AdminLogsHttpClient.GetLatest(_maxEntries);
        }
        catch (Exception ex)
        {
            _loadError = $"Failed to load logs: {ex.Message}";
        }

        await ConnectHub();
    }

    private async Task ConnectHub()
    {
        try
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(NavigationManager.ToAbsoluteUri("hubs/admin-logs"), options =>
                {
                    options.AccessTokenProvider = async () => (await LoginService.GetLoggedUser())?.Token;
                })
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<LogEntryDto[]>("LogsAppended", batch =>
            {
                if (batch is null || batch.Length == 0) return;
                _entries ??= [];
                var merged = batch.Concat(_entries)
                    .OrderByDescending(e => e.TimestampUtc)
                    .ThenByDescending(e => e.Id)
                    .Take(_maxEntries)
                    .ToList();
                _entries = merged;
                _ = InvokeAsync(StateHasChanged);
            });

            _hubConnection.Reconnected += async _ =>
            {
                if (_hubConnection is not null)
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

    private static Color LevelColor(LogSeverity level) => level switch
    {
        LogSeverity.Critical => Color.Error,
        LogSeverity.Error => Color.Error,
        LogSeverity.Warning => Color.Warning,
        _ => Color.Default,
    };

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }
}