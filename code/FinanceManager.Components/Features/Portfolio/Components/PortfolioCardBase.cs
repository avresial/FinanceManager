using FinanceManager.Components.Features.Portfolio.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Portfolio.Components;

public abstract class PortfolioCardBase<T> : ComponentBase, IDisposable where T : class
{
    private readonly RefreshVersionGate _gate = new();
    private (string Scope, DateTime Start, DateTime End)? _displayed;
    protected T? Model { get; private set; }
    protected bool Loading { get; private set; } = true;
    protected bool Failed { get; private set; }

    [Parameter] public string Height { get; set; } = "390px";
    [Parameter] public EventCallback<bool> RefreshingChanged { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISnapshotRefreshCoordinator Coordinator { get; set; }
    [Inject] public required ILogger<PortfolioCardBase<T>> Logger { get; set; }

    protected abstract Task<(string Key, Func<Task<T>> Fetch)> GetRequestAsync(int userId, DateTime start, DateTime end);

    protected async Task Refresh(DateTime start, DateTime end)
    {
        var version = _gate.Claim();
        Failed = false;
        try
        {
            await RefreshingChanged.InvokeAsync(true);
            var user = await LoginService.GetLoggedUser();
            if (!_gate.IsCurrent(version)) return;
            if (user is null)
            {
                Model = null;
                _displayed = null;
                return;
            }
            var (key, fetch) = await GetRequestAsync(user.UserId, start, end);
            if (!_gate.IsCurrent(version)) return;
            if (_displayed != (key, start.Date, end.Date))
            {
                Model = null;
                _displayed = (key, start.Date, end.Date);
            }
            Loading = Model is null;
            StateHasChanged();

            var result = await Coordinator.RunAsync(new SnapshotRefreshRequest<PortfolioCardSnapshot<T>, T>
            {
                Key = key,
                Gate = _gate,
                ClaimedVersion = version,
                ToModel = s => s.Scope == key && s.Start == start.Date && s.End == end.Date ? s.Model : null,
                ToSnapshot = model => new() { Scope = key, Start = start.Date, End = end.Date, Model = model },
                FetchAsync = async () => await fetch(),
                OnSnapshotPainted = Paint,
                OnRefreshed = Paint
            });
            if (_gate.IsCurrent(version)) Failed = result.Error is not null;
        }
        catch (Exception ex)
        {
            if (!_gate.IsCurrent(version)) return;
            Model = null;
            Failed = true;
            Logger.LogError(ex, "Could not resolve portfolio card settings.");
        }
        finally
        {
            if (_gate.IsCurrent(version))
            {
                Loading = false;
                await RefreshingChanged.InvokeAsync(false);
            }
        }
    }

    private Task Paint(T model)
    {
        Model = model;
        Loading = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _gate.Claim();
        GC.SuppressFinalize(this);
    }
}