using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FinanceManager.Components.Components.Charts;
public partial class LineChartJs : ComponentBase, IAsyncDisposable
{
    private IJSObjectReference? _chartInstance;

    [Inject] public required IJSRuntime JSRunTime { get; set; }
    [Parameter] public List<ChartJsLineDataPoint> Series { get; set; } = [];
    public string Id { get; set; } = "chartCanvas";
    CancellationTokenSource cancellationTokenSource = new();
    protected override void OnInitialized()
    {
        _cancellationToken = cancellationTokenSource.Token;
        base.OnInitialized();
    }


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
                        duration = 250
                    }

                }
            };

            _chartInstance = await JSRunTime.InvokeAsync<IJSObjectReference>("setupChart", _cancellationToken, Id, config);

            foreach (var newDataElement in Series)
            {
                if (_cancellationToken.IsCancellationRequested) break;
                await JSRunTime.InvokeVoidAsync("addDataPoint", _cancellationToken, _chartInstance, newDataElement);
                await Task.Delay(250);
            }

        }
    }
    bool isLoading = false;
    CancellationToken _cancellationToken = default(CancellationToken);
    protected override async Task OnParametersSetAsync()
    {
        if (_chartInstance is null) return;
        if (isLoading) return;
        isLoading = true;
        await JSRunTime.InvokeVoidAsync("clearDatasets", _cancellationToken, _chartInstance);

        foreach (var newDataElement in Series)
        {
            if (_cancellationToken.IsCancellationRequested) break;

            await JSRunTime.InvokeVoidAsync("addDataPoint", _cancellationToken, _chartInstance, newDataElement);
            await Task.Delay(200);
        }
        isLoading = false;

    }


    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        cancellationTokenSource.Cancel();
        if (_chartInstance is not null)
            await _chartInstance.DisposeAsync();
    }

}

public class ChartJsLineDataPoint
{
    public string x { get; set; }
    public decimal y { get; set; }

    public ChartJsLineDataPoint(DateTime x, decimal y)
    {
        this.x = x.ToString("yyyy-MM-dd");
        this.y = y;
    }

}