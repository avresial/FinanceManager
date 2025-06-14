using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FinanceManager.Components.Components.Charts;
public partial class LineChartJs : IAsyncDisposable
{
    private IJSObjectReference? _chartInstance;

    [Inject] public required IJSRuntime JSRunTime { get; set; }

    public string Id { get; set; } = "chartCanvas";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var config = new
            {
                Type = "line",
                //Options = new { ... },
                //Data = new { ... }
            };

            _chartInstance = await JSRunTime.InvokeAsync<IJSObjectReference>("setupChart", Id, config);
        }
    }
    public async Task UpdateChart()
    {
        await JSRunTime.InvokeVoidAsync("updateChart", _chartInstance, false);
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (_chartInstance is not null)
        {
            await _chartInstance.DisposeAsync();
        }
    }

}