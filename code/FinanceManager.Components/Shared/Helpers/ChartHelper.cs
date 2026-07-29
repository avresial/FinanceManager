namespace FinanceManager.Components.Shared.Helpers;

public static class ChartHelper
{
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
    /// Picks the y-axis tick formatter that suits a padded value range spread over
    /// <paramref name="tickCount"/> ticks. Compact "k" ticks only read well when the gap
    /// between ticks is itself hundreds-scale; on, say, a 300 PLN bond account every tick
    /// would otherwise collapse to the same "0.3k". Smaller ranges fall back to plain
    /// numbers carrying just enough decimals to keep adjacent ticks distinct.
    /// </summary>
    public static string GetYAxisTickFormatter(double minimum, double maximum, int tickCount)
    {
        var step = tickCount <= 0 ? 0 : (maximum - minimum) / tickCount;
        if (step >= 100) return CompactCurrencyTickFormatter;

        var decimals = step >= 10 ? 0 : step >= 1 ? 1 : 2;
        return "function(v){ return v.toLocaleString('en-US',{minimumFractionDigits:" + decimals +
               ",maximumFractionDigits:" + decimals + "}); }";
    }

    public static string GetCurrencyFormatter(string currency)
    {
        return @"function(value, opts) {
                    if (value === undefined) {return '';}
                    return Number(value).toLocaleString() + " + $" ' {currency}' " + ";}";
    }

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
}