using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Users;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards;

public partial class FinancialInsightsCarousel : IDisposable
{
    private bool _isLoading;
    private List<FinancialInsight> _insights = [];
    private int _currentIndex;
    private bool _isPaused;
    private Timer? _advanceTimer;

    private const int _dwellMs = 6000;

    [Inject] public required FinancialInsightsHttpClient FinancialInsightsHttpClient { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public int Count { get; set; } = 3;
    [Parameter] public int? AccountId { get; set; }

    private FinancialInsight? CurrentInsight =>
        _insights.Count > 0 ? _insights[Math.Min(_currentIndex, _insights.Count - 1)] : null;

    protected override async Task OnInitializedAsync() => await LoadInsightsAsync();

    protected override async Task OnParametersSetAsync() => await LoadInsightsAsync();

    private async Task LoadInsightsAsync()
    {
        _isLoading = true;
        StopAutoAdvance();
        try
        {
            _insights = await FinancialInsightsHttpClient.GetLatestAsync(Count, AccountId);
            _currentIndex = 0;
        }
        catch
        {
            Snackbar.Add("Unable to load financial insights.", Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StartAutoAdvance();
        }
    }

    private void GoPrevious()
    {
        if (_insights.Count <= 1) return;
        _currentIndex = (_currentIndex - 1 + _insights.Count) % _insights.Count;
        StartAutoAdvance();
    }

    private void GoNext()
    {
        if (_insights.Count <= 1) return;
        _currentIndex = (_currentIndex + 1) % _insights.Count;
        StartAutoAdvance();
    }

    private void OnMouseEnter() { _isPaused = true; StopAutoAdvance(); }
    private void OnMouseLeave() { _isPaused = false; StartAutoAdvance(); }
    private void OnFocusIn() { _isPaused = true; StopAutoAdvance(); }
    private void OnFocusOut() { _isPaused = false; StartAutoAdvance(); }

    private void StartAutoAdvance()
    {
        StopAutoAdvance();
        if (_insights.Count <= 1 || _isPaused) return;
        _advanceTimer = new Timer(OnAutoAdvance, null, _dwellMs, Timeout.Infinite);
    }

    private void StopAutoAdvance()
    {
        _advanceTimer?.Dispose();
        _advanceTimer = null;
    }

    private void OnAutoAdvance(object? _) =>
        InvokeAsync(() =>
        {
            _currentIndex = (_currentIndex + 1) % _insights.Count;
            StartAutoAdvance();
            StateHasChanged();
        });

    private static string GetRelativeTime(DateTime createdAt)
    {
        var elapsed = DateTime.UtcNow - createdAt.ToUniversalTime();
        if (elapsed.TotalMinutes < 1) return "just now";
        if (elapsed.TotalHours < 1) return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
        if (elapsed.TotalDays < 7) return $"{(int)elapsed.TotalDays}d ago";
        return $"{(int)(elapsed.TotalDays / 7)}w ago";
    }

    private static IEnumerable<string> GetTags(string tags) =>
        tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void Dispose() => StopAutoAdvance();
}
