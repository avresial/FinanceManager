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

public partial class AdminLogsPage : ComponentBase, IAsyncDisposable
{
    private List<LogEntryDto> _page = [];
    private int _totalCount;
    private int _skip;
    private const int _take = 25;
    private string _levelFilter = "all";
    private bool _loading;
    private string? _loadError;
    private HubConnection? _hubConnection;
    private readonly HashSet<int> _expanded = [];

    [Inject] public required AdminLogsHttpClient AdminLogsHttpClient { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    private int CurrentPage => _totalCount == 0 ? 1 : (_skip / _take) + 1;
    private int TotalPages => _totalCount == 0 ? 1 : (int)Math.Ceiling(_totalCount / (double)_take);
    private int FromDisplay() => _totalCount == 0 ? 0 : _skip + 1;
    private int ToDisplay() => Math.Min(_skip + _take, _totalCount);

    protected override async Task OnInitializedAsync()
    {
        await LoadPage();
        await ConnectHub();
    }

    private async Task LoadPage()
    {
        _loading = true;
        try
        {
            var levelParam = _levelFilter == "all" ? null : _levelFilter;
            var result = await AdminLogsHttpClient.GetPaged(_skip, _take, levelParam);
            _page = result.Items.ToList();
            _totalCount = result.TotalCount;
            _loadError = null;
        }
        catch (Exception ex)
        {
            _loadError = $"Failed to load logs: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task OnLevelChanged(string? value)
    {
        _levelFilter = string.IsNullOrEmpty(value) ? "all" : value;
        _skip = 0;
        _expanded.Clear();
        await LoadPage();
    }

    private async Task PrevPage()
    {
        _skip = Math.Max(0, _skip - _take);
        _expanded.Clear();
        await LoadPage();
    }

    private async Task NextPage()
    {
        if (_skip + _take >= _totalCount) return;
        _skip += _take;
        _expanded.Clear();
        await LoadPage();
    }

    private bool IsExpanded(int id) => _expanded.Contains(id);

    private void ToggleExpand(int id)
    {
        if (!_expanded.Add(id))
            _expanded.Remove(id);
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
                // Only refresh when viewing the first page so live updates don't
                // shift the user's scroll position while they're paging history.
                if (_skip == 0)
                    _ = InvokeAsync(LoadPage);
            });

            _hubConnection.Reconnected += async _ =>
            {
                if (_hubConnection is not null)
                    await _hubConnection.InvokeAsync("Subscribe");
            };

            await _hubConnection.StartAsync();
            await _hubConnection.InvokeAsync("Subscribe");
        }
        catch
        {
            // Live updates are optional — page still works without them.
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