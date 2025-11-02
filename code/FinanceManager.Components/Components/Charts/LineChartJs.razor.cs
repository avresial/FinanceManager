using FinanceManager.Domain.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace FinanceManager.Components.Components.Charts;
public partial class LineChartJs : ComponentBase, IAsyncDisposable
{
    private IJSObjectReference? _chartInstance;
    private CancellationTokenSource _cancellationTokenSource = new();

    [Inject] public required IJSRuntime JSRunTime { get; set; }
    [Inject] public required ILogger Logger { get; set; }

    [Parameter] public List<List<ChartJsLineDataPoint>> Series { get; set; } = [];
    [Parameter] public List<string> ColorPallet { get; set; } = [];

    public string Id { get; set; } = Guid.NewGuid().ToString();


    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var config = new
            {
                Type = "line",
                Options = new
                {
                    Fill = false,
                    Interaction = new
                    {
                        Intersect = false
                    },
                    Radius = 0,
                    Responsive = true,
                    MaintainAspectRatio = false,
                    Scales = new
                    {
                        X = new
                        {
                            Type = "time",
                            Time = new
                            {
                                Unit = "month"
                            },
                            Display = false,
                            Border = new
                            {
                                Display = false
                            },
                            Grid = new
                            {
                                Display = false
                            },
                        },
                        Y = new
                        {
                            Display = false,
                            Border = new
                            {
                                Display = false
                            },
                            Grid = new
                            {
                                Display = false
                            }
                        }
                    },
                    Plugins = new
                    {
                        Legend = new
                        {
                            Display = false,
                        }
                    },
                    Animation = new
                    {
                        duration = 50
                    }

                }
            };

            if (ColorPallet.Count == 0) ColorPallet = ColorsProvider.GetColors();

            List<Dataset> datasets = [];
            for (int i = 0; i < Series.Count; i++)
            {
                datasets.Add(new Dataset()
                {
                    BackgroundColor = ColorPallet[i % ColorPallet.Count] + "80",
                    BorderColor = ColorPallet[i % ColorPallet.Count]
                });
            }

            _chartInstance = await JSRunTime.InvokeAsync<IJSObjectReference>("setupChart", _cancellationTokenSource.Token, Id, config, datasets);


            List<List<ChartJsLineDataPoint>> newSeries = [];
            try
            {
                foreach (var serie in Series)
                {
                    List<ChartJsLineDataPoint> singleSerie = [];
                    foreach (var element in serie)
                        singleSerie.Add(element);
                    newSeries.Add(singleSerie);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, ex.Message);
            }
            await DisplayData(_cancellationTokenSource.Token, newSeries);

        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_chartInstance is null) return;

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();

        List<List<ChartJsLineDataPoint>> newSeries = [];
        try
        {
            foreach (var serie in Series)
            {
                List<ChartJsLineDataPoint> singleSerie = [];

                foreach (var element in serie)
                    singleSerie.Add(element);

                newSeries.Add(singleSerie);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, ex.Message);
        }

        await DisplayData(_cancellationTokenSource.Token, newSeries);
    }

    private async Task DisplayData(CancellationToken cancellationToken, List<List<ChartJsLineDataPoint>> newSeries)
    {
        await JSRunTime.InvokeVoidAsync("clearDatasets", cancellationToken, _chartInstance);

        foreach (var serie in newSeries)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (serie.Count <= 0) continue;

            int delay = 1000 * 5 / serie.Count;
            if (delay > 100) delay = 100;
            foreach (var newDataElement in serie)
            {
                if (cancellationToken.IsCancellationRequested) break;
                var index = newSeries.IndexOf(serie);
                if (index < 0) break;

                await JSRunTime.InvokeVoidAsync("addDataPoint", cancellationToken, _chartInstance, index, newDataElement);

                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, ex.Message);
                }
            }
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        _cancellationTokenSource.Cancel();
        if (_chartInstance is not null)
            await _chartInstance.DisposeAsync();
    }

}

public class ChartJsLineDataPoint(DateTime x, decimal y)
{
    public string x { get; set; } = x.ToString("yyyy-MM-dd");
    public decimal y { get; set; } = y;
}

public class Dataset
{
    public string BorderColor { get; set; } = "#FFAB00";
    public string BackgroundColor { get; set; } = "#FFAB0080";
    public string Fill { get; set; } = "origin";

}