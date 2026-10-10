using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Shared.Helpers;

public static class ChartHelper
{
    /// <summary>
    /// Carries the final known value through the requested chart end date when a bucketed
    /// cumulative series ends at the first date of its final bucket. The source sequence is not
    /// mutated, and an empty series or one that already reaches the end is returned unchanged.
    /// </summary>
    public static List<TimeSeriesModel> EnsureSeriesEndsAt(IEnumerable<TimeSeriesModel> series, DateTime endDate)
    {
        var ordered = series.OrderBy(point => point.DateTime).ToList();
        if (ordered.Count == 0) return ordered;

        var targetDate = endDate.Date;
        var last = ordered[^1];
        if (last.DateTime.Date >= targetDate) return ordered;

        ordered.Add(new TimeSeriesModel(targetDate, last.Value, last.Name));
        return ordered;
    }

    /// <summary>
    /// Aligns multiple displayed series to one ordered timeline. Missing points carry the latest
    /// known value so a shared tooltip can show every series at each date. Dates before a series
    /// starts use its first value because no previous value exists. Actual source points are reused
    /// and the input sequences are not mutated.
    /// </summary>
    public static List<List<TimeSeriesModel>> AlignSeries(IEnumerable<IEnumerable<TimeSeriesModel>> series)
    {
        ArgumentNullException.ThrowIfNull(series);

        var orderedSeries = series
            .Select(points => points.OrderBy(point => point.DateTime).ToList())
            .ToList();
        var dates = orderedSeries
            .SelectMany(points => points)
            .Select(point => point.DateTime)
            .Distinct()
            .Order()
            .ToList();

        return orderedSeries.Select(points =>
        {
            if (points.Count == 0 || dates.Count == 0)
                return points;

            var pointsByDate = points
                .GroupBy(point => point.DateTime)
                .ToDictionary(group => group.Key, group => group.Last());
            var first = points[0];
            var previous = first;

            return dates.Select(date =>
            {
                if (pointsByDate.TryGetValue(date, out var actual))
                {
                    previous = actual;
                    return actual;
                }

                var carried = date < first.DateTime ? first : previous;
                return new TimeSeriesModel(date, carried.Value, carried.Name);
            }).ToList();
        }).ToList();
    }

    /// <summary>
    /// Compact currency tick labels for a y-axis: 2.5k / 7.5k / 10k / 13k. Shared by the
    /// dashboard time-series cards and the account details hero chart so both label their
    /// value axis identically. Zero is labelled "0" rather than "0k"; only non-numeric
    /// values are dropped, so an axis crossing zero still labels that gridline.
    /// </summary>
    public const string CompactCurrencyTickFormatter =
        "function(v){ if(!Number.isFinite(v)) return ''; if(v===0) return '0'; var k=v/1000; " +
        "return (Math.abs(k)<10 && k%1!==0 ? k.toFixed(1) : Math.round(k))+'k'; }";

    /// <summary>
    /// Compact currency tick labels that always carry one decimal: 21.4k / 21.6k / 21.8k. Used when
    /// ticks are hundreds apart, where <see cref="CompactCurrencyTickFormatter"/> rounds values of
    /// 10k and more to whole thousands and adjacent ticks collapse into the same label.
    /// </summary>
    public const string CompactCurrencyTickFormatterOneDecimal =
        "function(v){ if(!Number.isFinite(v)) return ''; if(v===0) return '0'; return (v/1000).toFixed(1)+'k'; }";

    /// <summary>
    /// Picks the y-axis tick formatter that suits a padded value range spread over
    /// <paramref name="tickCount"/> ticks. Compact "k" ticks only read well when the gap
    /// between ticks is itself hundreds-scale; on, say, a 300 PLN bond account every tick
    /// would otherwise collapse to the same "0.3k". Smaller ranges fall back to plain
    /// numbers carrying just enough decimals to keep adjacent ticks distinct.
    /// </summary>
    public static string GetYAxisTickFormatter(double minimum, double maximum, int tickCount)
    {
        var step = tickCount <= 0 ? 0 : (maximum - minimum) / tickCount;
        if (step >= 1000) return CompactCurrencyTickFormatter;
        if (step >= 100) return CompactCurrencyTickFormatterOneDecimal;

        var decimals = step >= 10 ? 0 : step >= 1 ? 1 : 2;
        return "function(v){ return v.toLocaleString('en-US',{minimumFractionDigits:" + decimals +
               ",maximumFractionDigits:" + decimals + "}); }";
    }

    /// <summary>
    /// ApexCharts tooltip formatter that renders money exactly like <see cref="MoneyFormatter.Format"/>:
    /// comma thousands grouping, two decimals and a regular space before the currency (<c>12,480.00 PLN</c>).
    /// The locale is fixed to en-US so the tooltip never depends on the browser locale.
    /// </summary>
    public static string GetCurrencyFormatter(string currency) =>
        "function(value){ if (value === undefined || value === null) { return ''; } " +
        "return Number(value).toLocaleString('en-US',{minimumFractionDigits:2,maximumFractionDigits:2}) + ' " + currency + "'; }";

    /// <summary>
    /// Pads a series' displayed value range by 5% on both ends so small changes stay visible
    /// instead of being flattened by a zero-based axis. A constant series (zero range) still
    /// gets a non-zero band so its line does not sit on the axis edge.
    /// </summary>
    public static (double Min, double Max) AddYRangePadding(double minimum, double maximum)
    {
        var range = maximum - minimum;
        var padding = range == 0 ? Math.Max(Math.Abs(minimum) * 0.05, 1) : range * 0.05;
        return (minimum - padding, maximum + padding);
    }

    /// <summary>
    /// Number of value bands a time-series card's y-axis aims for. Short cards (up to 300px, which
    /// includes the 250px default) get fewer bands so the tick labels, which float inside the plot,
    /// do not crowd the series; taller cards keep the finer grid. A height that is not a plain
    /// pixel value (e.g. a percentage) falls back to the short-card setting.
    /// </summary>
    public static int GetYTargetBands(string? cardHeight)
    {
        const int shortCardBands = 3;
        const int tallCardBands = 4;
        const double shortCardMaxPx = 300;

        var text = cardHeight?.Trim();
        if (text is not null && text.EndsWith("px", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(text[..^2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var px))
            return px <= shortCardMaxPx ? shortCardBands : tallCardBands;

        return shortCardBands;
    }

    /// <summary>
    /// Y scale for charts whose x-axis labels overlay the bottom of the plot: the padded range is
    /// snapped to round 1/2/5 tick steps, then one extra empty band is added below the data so the
    /// overlaid date labels never sit on top of the series. The bottom tick of that band is the
    /// gutter, not data, so its label is expected to be hidden.
    /// </summary>
    public static (double Min, double Max, int TickAmount) GetYScaleWithLabelGutter(double minimum, double maximum, int targetBands = 4)
    {
        var (low, high) = AddYRangePadding(minimum, maximum);
        var step = GetNiceStep((high - low) / targetBands);
        var niceLow = Math.Floor(low / step) * step;
        var niceHigh = Math.Ceiling(high / step) * step;
        var bands = (int)Math.Round((niceHigh - niceLow) / step);

        return (niceLow - step, niceHigh, bands + 1);
    }

    private static double GetNiceStep(double rawStep)
    {
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        var fraction = rawStep / magnitude;
        var nice = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
        return nice * magnitude;
    }
}