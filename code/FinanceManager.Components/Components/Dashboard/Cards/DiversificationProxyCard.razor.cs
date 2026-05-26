using ApexCharts;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards;

public partial class DiversificationProxyCard
{
    private const int _lowSubScoreThreshold = 16;
    private const int _highSubScoreThreshold = 34;

    private bool _isLoading;
    private DiversificationScore? _score;
    private MudBlazor.Color _bandColor = MudBlazor.Color.Default;
    private List<string> _explanations = [];
    private List<ScorePoint> _series = [];
    private ApexChartOptions<ScorePoint>? _chartOptions;

    [Parameter] public string Height { get; set; } = "300px";

    [Inject] public required ILogger<DiversificationProxyCard> Logger { get; set; }
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

            _score = await DiversificationHttpClient.GetDiversificationScore(user.UserId, DateTime.UtcNow);
            if (_score is null) return;

            _bandColor = _score.Band switch
            {
                "Broad" => MudBlazor.Color.Success,
                "Moderate" => MudBlazor.Color.Warning,
                _ => MudBlazor.Color.Error
            };
            var arcColor = _score.Band switch
            {
                "Broad" => "var(--mud-palette-success)",
                "Moderate" => "var(--mud-palette-warning)",
                _ => "var(--mud-palette-error)"
            };
            _series = [new ScorePoint(_score.Band, _score.Score)];
            _chartOptions = BuildChartOptions(arcColor);
            _explanations = BuildExplanations(_score);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading diversification score");
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private static ApexChartOptions<ScorePoint> BuildChartOptions(string arcColor) => new()
    {
        Chart = new Chart
        {
            FontFamily = "Roboto, sans-serif"
        },
        PlotOptions = new PlotOptions
        {
            RadialBar = new PlotOptionsRadialBar
            {
                StartAngle = -135,
                EndAngle = 135,
                Hollow = new Hollow { Size = "62%" },
                Track = new Track
                {
                    Background = "var(--mud-palette-lines-default)",
                    StrokeWidth = "100%"
                },
                DataLabels = new RadialBarDataLabels
                {
                    Name = new RadialBarDataLabelsName
                    {
                        Show = true,
                        FontSize = "13px",
                        OffsetY = 22,
                        Color = "var(--mud-palette-text-secondary)"
                    },
                    Value = new RadialBarDataLabelsValue
                    {
                        Show = true,
                        FontSize = "40px",
                        FontWeight = "700",
                        OffsetY = -10,
                        Color = "var(--mud-palette-text-primary)"
                    }
                }
            }
        },
        Stroke = new Stroke { LineCap = LineCap.Round },
        Colors = [arcColor]
    };

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

    internal record ScorePoint(string Label, int Value);
}