using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Insights.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Globalization;

namespace FinanceManager.Components.Components.Features.Dashboard.Cards;

public partial class DiversificationProxyCard
{
    private const int _lowSubScoreThreshold = 16;
    private const int _highSubScoreThreshold = 34;

    // Material Filled icon paths used for the gentle insight callout.
    private const string _insightGlyph = "M12 2a7 7 0 0 0-7 7c0 2.38 1.19 4.47 3 5.74V17a1 1 0 0 0 1 1h6a1 1 0 0 0 1-1v-2.26c1.81-1.27 3-3.36 3-5.74a7 7 0 0 0-7-7M9 21a1 1 0 0 0 1 1h4a1 1 0 0 0 1-1v-1H9v1Z";
    private const string _checkGlyph = "M9 16.17 4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z";

    // Gauge geometry (semicircle, top half). Matches the design handoff.
    private const double _cx = 100, _cy = 96, _r = 78;

    private static readonly (string Key, string Label)[] _bands =
    [
        ("limited", "Limited"),
        ("moderate", "Moderate"),
        ("broad", "Broad"),
    ];

    private bool _isLoading;
    private DiversificationScore? _score;
    private List<string> _explanations = [];
    private string _bandKey = "limited";
    private string _bandStyle = "";
    private string _insightGlyphPath = _insightGlyph;
    private string _trackPath = "";
    private string _valuePath = "";
    private List<(string X, string Y)> _ticks = [];

    private int? _userId;
    private DateTime _asOfDate = DateTime.UtcNow;
    private bool _showDetails;
    private bool _breakdownLoading;
    private DiversificationBreakdown? _breakdown;

    [Parameter] public string Height { get; set; } = "300px";

    [Inject] public required ILogger<DiversificationProxyCard> Logger { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required DiversificationHttpClient DiversificationHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null)
            {
                _score = null;
                return;
            }

            _userId = user.UserId;
            _asOfDate = DateTime.UtcNow;
            _score = await DiversificationHttpClient.GetDiversificationScore(_userId.Value, _asOfDate);
            if (_score is null) return;

            _bandKey = _score.Band.ToLowerInvariant();
            _bandStyle = BandVars(_bandKey);
            _insightGlyphPath = _bandKey == "broad" ? _checkGlyph : _insightGlyph;
            _explanations = BuildExplanations(_score);
            BuildGauge(_score.Score);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading diversification score");
            Snackbar.Add("Unable to load diversification score.", Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    // Inline-style custom properties so a single band drives every band-coloured element.
    private static string BandVars(string key) => $"--c:var(--band-{key});--cs:var(--band-{key}-soft);";

    private async Task ShowDetails()
    {
        _showDetails = true;

        if (_breakdown is not null || _userId is null) return;

        _breakdownLoading = true;
        try
        {
            _breakdown = await DiversificationHttpClient.GetDiversificationBreakdown(_userId.Value, _asOfDate);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading diversification breakdown");
            Snackbar.Add("Unable to load diversification breakdown.", Severity.Error);
        }
        finally
        {
            _breakdownLoading = false;
        }
    }

    private void HideDetails() => _showDetails = false;

    private void BuildGauge(int score)
    {
        var fraction = Math.Clamp(score / 100.0, 0, 1);
        _trackPath = Arc(180, 360);
        _valuePath = Arc(180, 180 + fraction * 180);
        _ticks = [.. new[] { 40.0, 70.0 }.Select(t => Polar(180 + (t / 100.0) * 180))];
    }

    private static (string X, string Y) Polar(double degrees)
    {
        var angle = degrees * Math.PI / 180.0;
        return (F(_cx + _r * Math.Cos(angle)), F(_cy + _r * Math.Sin(angle)));
    }

    private static string Arc(double startDeg, double endDeg)
    {
        var (x0, y0) = Polar(startDeg);
        var (x1, y1) = Polar(endDeg);
        var largeArc = endDeg - startDeg > 180 ? 1 : 0;
        return $"M {x0} {y0} A {F(_r)} {F(_r)} 0 {largeArc} 1 {x1} {y1}";
    }

    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    internal static List<string> BuildExplanations(DiversificationScore score)
    {
        var explanations = new List<string>();

        switch (score.Band)
        {
            case "Limited":
                if (score.AssetClassScore <= _lowSubScoreThreshold)
                    explanations.Add("Limited asset-class spread — most holdings sit in one or two classes.");
                if (score.HoldingsScore <= _lowSubScoreThreshold)
                    explanations.Add("Few unique holdings — concentration risk on a small number of positions.");
                break;
            case "Broad":
                if (score.AssetClassScore >= _highSubScoreThreshold)
                    explanations.Add("Broad asset-class spread — holdings span many investment types.");
                if (score.HoldingsScore >= _highSubScoreThreshold)
                    explanations.Add("Many unique holdings — wide breadth across individual positions.");
                break;
        }

        return explanations;
    }
}