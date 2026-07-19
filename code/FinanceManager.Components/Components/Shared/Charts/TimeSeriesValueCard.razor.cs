using ApexCharts;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Shared.Charting;
using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace FinanceManager.Components.Components.Shared.Charts;

/// <summary>
/// Shared dashboard time-series card: a MudCard with a value/delta readout header and a
/// full-bleed ApexCharts gradient-area chart (axes, gridlines, hover tooltip). Hovering a
/// point syncs the header to that point. The four time-series cards (assets, net worth,
/// liabilities, investment type) are thin wrappers that load data and feed it in here.
/// </summary>
public partial class TimeSeriesValueCard
{
    private enum LoadState { Loading, Ready, Empty, Error }

    private ApexChart<TimeSeriesModel>? _chart;
    private ApexChartOptions<TimeSeriesModel>? _options;
    private int? _hoverIndex;
    private string? _builtForAccent;
    private string? _builtForCurrency;

    // ApexCharts draws via JS interop only when the ApexChart component is first
    // rendered; feeding new Data into an already-rendered chart changes nothing on
    // screen. Track which chart instance drew which points so OnAfterRenderAsync can
    // ask the live chart to redraw when the series content actually changed
    // (e.g. the dashboard reloading all cards after a date-range change).
    private ApexChart<TimeSeriesModel>? _drawnChart;
    private List<(DateTime DateTime, decimal Value)> _drawnPoints = [];

    /// <summary>Card title shown top-left (e.g. "Assets value over time").</summary>
    [Parameter] public string Title { get; set; } = string.Empty;

    /// <summary>Series points, ascending by <see cref="TimeSeriesModel.DateTime"/>.</summary>
    [Parameter] public IReadOnlyList<TimeSeriesModel> Data { get; set; } = [];

    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public bool HasError { get; set; }

    /// <summary>Invoked by the error-state "Retry" button. When unset, no button is shown.</summary>
    [Parameter] public EventCallback OnRetry { get; set; }

    /// <summary>Series + readout accent colour. Defaults to the brand gold.</summary>
    [Parameter] public string Accent { get; set; } = ColorsProvider.GetColors().First();

    /// <summary>
    /// When true a falling series is "good" (liabilities): the delta chip turns green while
    /// the line drops. The triangle/sign still follow the real direction of change.
    /// </summary>
    [Parameter] public bool DeltaPositiveWhenFalling { get; set; }

    /// <summary>Currency suffix for the readout / tooltip (e.g. "PLN").</summary>
    [Parameter] public string CurrencyShortName { get; set; } = "PLN";

    /// <summary>
    /// Optional hero value shown in the header while not hovering. When set it overrides the
    /// latest-point value (e.g. a period total for the net cash flow card); hovering a point
    /// still reveals that point's value. When null the latest point is shown (net worth,
    /// closing balance, …).
    /// </summary>
    [Parameter] public decimal? SummaryValue { get; set; }

    /// <summary>Caption shown beside <see cref="SummaryValue"/> while not hovering.</summary>
    [Parameter] public string? SummaryCaption { get; set; }

    /// <summary>Card height; fills its grid cell. Default 250px (issue #284).</summary>
    [Parameter] public string Height { get; set; } = "250px";

    private LoadState State =>
        IsLoading ? LoadState.Loading
        : HasError ? LoadState.Error
        : Data.Count == 0 ? LoadState.Empty
        : LoadState.Ready;

    private bool IsHovering => _hoverIndex is not null;

    // At rest an optional summary (e.g. a period total) overrides the latest-point hero;
    // hovering always falls back to the hovered point so the chart stays explorable.
    private bool ShowSummary => SummaryValue is not null && !IsHovering;

    private decimal HeaderValue => ShowSummary ? SummaryValue!.Value : Shown.Value;

    private string CaptionText =>
        ShowSummary && !string.IsNullOrEmpty(SummaryCaption)
            ? SummaryCaption!
            : $"{(IsHovering ? "Value on" : "Current value")} {(char)0x00B7} " +
              Shown.DateTime.ToLocalTime().ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    // The delta chip compares the shown point to the range start; that pairing is
    // meaningless next to a summary hero (a period sum), so it's hidden whenever a
    // summary value is supplied.
    private bool ShowDelta => HasDelta && SummaryValue is null;

    // The point reflected in the header: hovered, else the latest.
    private TimeSeriesModel Shown =>
        Data.Count == 0
            ? new TimeSeriesModel(DateTime.Today, 0m)
            : Data[Math.Clamp(_hoverIndex ?? Data.Count - 1, 0, Data.Count - 1)];

    // Percent change is only meaningful when the range opens with a non-zero
    // baseline; from a zero baseline the change is undefined (division by zero),
    // so the chip is hidden rather than showing a misleading "+0.0 %".
    private bool HasDelta => Data.Count > 0 && Data[0].Value != 0;

    // Percent change of the shown point versus the first point in range.
    private double DeltaPct =>
        Data.Count == 0 || Data[0].Value == 0
            ? 0
            : (double)((Shown.Value - Data[0].Value) / Math.Abs(Data[0].Value)) * 100;

    private bool DeltaIsUp => DeltaPct >= 0;
    private bool DeltaIsGood => DeltaPositiveWhenFalling ? DeltaPct <= 0 : DeltaPct >= 0;
    private string DeltaColor => DeltaIsGood ? "#66BB6A" : "#EF5350";
    private string DeltaBackground => DeltaIsGood ? "rgba(102,187,106,0.15)" : "rgba(239,83,80,0.15)";

    private string DeltaText
    {
        get
        {
            // Glyphs built from code points so the source stays pure-ASCII (encoding-safe):
            // U+25B2 up triangle / U+25BC down triangle / U+2212 minus sign.
            var glyph = ((char)(DeltaIsUp ? 0x25B2 : 0x25BC)).ToString();
            var sign = DeltaIsUp ? "+" : ((char)0x2212).ToString();
            return $"{glyph} {sign}{Math.Abs(DeltaPct).ToString("F1", CultureInfo.InvariantCulture)} %";
        }
    }

    protected override void OnParametersSet()
    {
        if (_options is null || _builtForAccent != Accent || _builtForCurrency != CurrencyShortName)
            BuildOptions();

        ApplyYScale();

        // Drop a stale hover index if the series shrank (or emptied) between loads.
        if (_hoverIndex is int i && i >= Data.Count)
            _hoverIndex = null;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (State != LoadState.Ready || _chart is null) return;

        var points = Data.Select(p => (p.DateTime, p.Value)).ToList();
        if (!ReferenceEquals(_chart, _drawnChart))
        {
            // A freshly created chart instance (first render, or remounted after a
            // loading/empty/error state) has just drawn the current data itself.
            _drawnChart = _chart;
            _drawnPoints = points;
            return;
        }

        if (points.SequenceEqual(_drawnPoints)) return;

        _drawnPoints = points;
        await _chart.RenderAsync();
    }

    // Pin the Y axis to a "nice" zero-based range so the card shows ~5 round
    // gridline labels (2.5k / 5k / …) like the design, instead of ApexCharts
    // collapsing forceNiceScale to one or two ticks. Always spans the zero
    // baseline so the area fill reads correctly for positive (assets) and
    // negative (liabilities) series alike.
    private void ApplyYScale()
    {
        var axis = _options?.Yaxis?.FirstOrDefault();
        if (axis is null || Data.Count == 0) return;

        double lo = Math.Min(0d, (double)Data.Min(p => p.Value));
        double hi = Math.Max(0d, (double)Data.Max(p => p.Value));
        if (hi <= lo) hi = lo + 1d;

        double rawStep = (hi - lo) / 5d;
        double mag = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        double norm = rawStep / mag;
        double step = (norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 2.5 ? 2.5 : norm <= 5 ? 5 : 10) * mag;

        double niceMin = Math.Floor(lo / step) * step;
        double niceMax = Math.Ceiling(hi / step) * step;
        int ticks = Math.Max(1, (int)Math.Round((niceMax - niceMin) / step));

        axis.Min = niceMin;
        axis.Max = niceMax;
        axis.TickAmount = ticks;
        axis.ForceNiceScale = false;
    }

    private void OnPointEnter(HoverData<TimeSeriesModel> hoverData)
    {
        var idx = hoverData.DataPointIndex;
        if (idx >= 0 && idx < Data.Count)
        {
            _hoverIndex = idx;
            StateHasChanged();
        }
    }

    private void OnPointLeave(HoverData<TimeSeriesModel> hoverData)
    {
        if (_hoverIndex is null) return;
        _hoverIndex = null;
        StateHasChanged();
    }

    // "12 480.00 PLN" - two decimals, non-breaking-space (U+00A0) thousands separator, currency suffix.
    private string FormatMoney(decimal value)
    {
        var nbsp = ((char)0x00A0).ToString();
        return value.ToString("N2", CultureInfo.InvariantCulture).Replace(",", nbsp) + nbsp + CurrencyShortName;
    }

    private void BuildOptions()
    {
        _builtForAccent = Accent;
        _builtForCurrency = CurrencyShortName;

        _options = new ApexChartOptions<TimeSeriesModel>
        {
            Chart = new Chart
            {
                Toolbar = new Toolbar { Show = false },
                Zoom = new Zoom { Enabled = false },
                Background = "transparent",
                FontFamily = "Roboto, sans-serif",
                Animations = new Animations { Enabled = true, Speed = 400 },
            },
            Colors = [Accent],
            Stroke = new Stroke { Curve = Curve.Smooth, Width = 2.5, LineCap = LineCap.Round },
            DataLabels = new DataLabels { Enabled = false },
            Legend = new Legend { Show = false },
            Fill = new Fill
            {
                Type = [FillType.Gradient],
                Gradient = new FillGradient
                {
                    ShadeIntensity = 1,
                    // 2-stop gradient (no intermediate stop): opacity always starts at
                    // 0.45 at the very top of the SVG and fades to 0 at the bottom.
                    // With 3 stops [0,62,100] the gradient reached 0 at 62% of chart
                    // height; when the line was high (right side of chart) the fill
                    // covered the full height but the visible band was already near-zero
                    // opacity – making the right side look empty. 2 stops keeps the
                    // gradient proportional to the fill area for any line position.
                    OpacityFrom = 0.45,
                    OpacityTo = 0d,
                    Stops = [0, 100],
                },
            },
            Markers = new Markers
            {
                // Size / StrokeWidth are set to 0.1 (non-zero so the serializer includes
                // them). 0.1 px is sub-pixel – invisible at rest. On hover ApexCharts
                // resizes to Hover.Size = 5 making the dot visible. The previous CSS
                // display:none approach also hid the hover dot.
                Size = 0.1,
                StrokeWidth = 0.1,
                Colors = [Accent],
                StrokeColors = "#23232a",
                Hover = new MarkersHover { Size = 5 },
            },
            Grid = new Grid
            {
                // Theme-neutral grey: the app follows the system light/dark preference,
                // so gridlines/labels must read on both a light and a dark card surface.
                BorderColor = "rgba(128,128,128,0.18)",
                StrokeDashArray = 0,
                Padding = new Padding { Left = 0, Right = 0, Top = 4, Bottom = 0 },
                Xaxis = new GridXAxis { Lines = new Lines { Show = false } },
                Yaxis = new GridYAxis { Lines = new Lines { Show = true } },
            },
            Xaxis = new XAxis
            {
                Type = XAxisType.Datetime,
                // No fixed TickAmount: let ApexCharts place datetime ticks on natural
                // boundaries. A fixed count lands ticks mid-month and repeats labels
                // (e.g. "Jan '26 Jan '26") when the series has fewer points than ticks.
                AxisBorder = new AxisBorder { Show = false },
                AxisTicks = new AxisTicks { Show = false },
                Crosshairs = new AxisCrosshairs { Show = true, Width = 1 },
                Labels = new XAxisLabels
                {
                    // The chart's inner div is pushed 30px below the card (see the
                    // .razor) so ApexCharts' reserved x-axis row is clipped and the
                    // gradient sits flush with the bottom border. The month labels live
                    // in that clipped row; OffsetY lifts them back up so they sit just
                    // inside the card's bottom edge (bottom-aligned). The chart SVG is
                    // rendered at 2x, so each unit of OffsetY shifts the label ~2px:
                    // -16 lands the labels ~6px above the card bottom. (0 dropped them
                    // below the clip; -34 floated them ~40px up into the plot.)
                    OffsetY = -16,
                    Style = new AxisLabelStyle { Colors = "rgba(130,130,130,0.95)", FontSize = "11px" },
                    // Format per tick granularity so sub-month ranges don't repeat the
                    // month (e.g. a "This month" daily range showed "Jun '26" on every tick,
                    // and finer-than-month ticks duplicated "Jan '26 Jan '26"). Day ticks now
                    // carry the day, month ticks the month+year, year ticks the year.
                    DatetimeFormatter = new DatetimeFormatter { Year = "yyyy", Month = "MMM 'yy", Day = "dd MMM" },
                },
            },
            Yaxis =
            [
                new YAxis
                {
                    // Min / Max / TickAmount are set per-series in ApplyYScale.
                    // Floating = true: labels overlay the plot area instead of
                    // occupying a reserved column to the left. This eliminates the
                    // dead space on the left edge so the chart fills the card fully.
                    Floating = true,
                    AxisBorder = new AxisBorder { Show = false },
                    AxisTicks = new AxisTicks { Show = false },
                    Labels = new YAxisLabels
                    {
                        // Floating labels overlay the plot; OffsetX nudges them right
                        // off the left chart edge so they sit just inside the card's
                        // left border (~7px gap), bottom-aligned-style like the x-axis
                        // labels. Each unit of OffsetX is ~1px here, so 24 ≈ 7px in.
                        OffsetX = 24,
                        Style = new AxisLabelStyle { Colors = "rgba(130,130,130,0.95)", FontSize = "11px" },
                        // compact currency ticks: 2.5k / 7.5k / 10k / 13k
                        Formatter = "function(v){ if(!v) return ''; var k=v/1000; " +
                                    "return (Math.abs(k)<10 && k%1!==0 ? k.toFixed(1) : Math.round(k))+'k'; }",
                    },
                },
            ],
            Tooltip = new Tooltip
            {
                Enabled = true,
                Theme = Mode.Dark,
                // Pin the tooltip to the plot's top-left corner. The card is
                // overflow:hidden (it clips the pushed-out x-axis label row), which
                // would also clip a cursor-following tooltip near the chart edges. A
                // fixed tooltip always renders inside the visible plot area while the
                // crosshair + marker dot still track the hovered point. Content stays
                // per-point: month title + formatted value.
                Fixed = new TooltipFixed { Enabled = true, Position = TooltipPosition.TopLeft, OffsetX = 8, OffsetY = 8 },
                X = new TooltipX { Format = "MMMM yyyy" },
                Y = new TooltipY
                {
                    Formatter = "function(v){ return v.toLocaleString('en-US',{minimumFractionDigits:2,maximumFractionDigits:2})" +
                                ".replace(/,/g,'\\u00A0')+'\\u00A0" + CurrencyShortName + "'; }",
                },
                Marker = new TooltipMarker { Show = true },
            },
        };
    }
}