using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;

public partial class PortfolioReturnAttributionCard
{
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Parameter] public string Height { get; set; } = "380px";
    [Parameter] public PortfolioReturnAttributionCardModel? Model { get; set; }
    [Parameter] public Currency Currency { get; set; } = DefaultCurrency.PLN;
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public bool HasError { get; set; }
    [Parameter] public EventCallback Retry { get; set; }

    internal string UnsupportedComponentsText => Model is { UnsupportedComponents.Count: > 0 }
        ? string.Join("; ", Model.UnsupportedComponents)
        : "none";

    internal string StatusLabel => Model?.Status switch
    {
        PortfolioReturnAttributionStatus.InsufficientData => "Insufficient data",
        _ => "Attribution unavailable",
    };

    internal string StatusText => Model?.Status switch
    {
        PortfolioReturnAttributionStatus.InsufficientData => "Add investment history or select a range with portfolio activity.",
        _ => "Prices or historical exchange rates are missing for this range.",
    };

    internal string RangeText => DateFormatter.FormatRange(StartDateTime, EndDateTime);

    private IReadOnlyList<AttributionStep> AttributionSteps
    {
        get
        {
            if (Model is null)
                return [];

            var components = new (string Label, decimal Value)[]
            {
                ("External cash movement", Model.ExternalCashMovement ?? 0m),
                ("Market / valuation effect", Model.MarketEffect ?? 0m),
                ("FX effect", Model.FxEffect ?? 0m),
                ("Known transaction fees", Model.FeeEffect ?? 0m),
            };

            var steps = new List<AttributionStep>(components.Length);
            var runningTotal = 0m;
            foreach (var component in components.OrderByDescending(component => component.Value))
            {
                var start = runningTotal;
                runningTotal += component.Value;
                steps.Add(new(component.Label, component.Value, start, runningTotal));
            }

            return steps;
        }
    }

    private IReadOnlyList<AttributionColumn> ChartColumns
    {
        get
        {
            var columns = AttributionSteps
                .Select(step => new AttributionColumn(step.Label, step.Value, step.Start, step.End, false))
                .ToList();

            if (Model?.TotalChange is decimal totalChange)
                columns.Add(new("End", totalChange, 0m, totalChange, true));

            return columns;
        }
    }

    private ChartAxisScale ChartScale
    {
        get
        {
            var columns = ChartColumns;
            if (columns.Count == 0)
                return new(-1m, 1m, 0.5m);

            var minimum = Math.Min(0m, columns.Min(column => Math.Min(column.Start, column.End)));
            var maximum = Math.Max(0m, columns.Max(column => Math.Max(column.Start, column.End)));
            if (minimum == maximum)
                return new(-1m, 1m, 0.5m);

            var range = maximum - minimum;
            var interval = NiceInterval(range);
            try
            {
                var axisMinimum = decimal.Floor(minimum / interval) * interval;
                var axisMaximum = decimal.Ceiling(maximum / interval) * interval;
                if (axisMinimum == axisMaximum)
                    axisMaximum += interval;

                return new(axisMinimum, axisMaximum, interval);
            }
            catch (OverflowException)
            {
                return new(minimum, maximum, range / 4m);
            }
        }
    }

    private string AttributionSummary => $"Starting at {FormatAmount(0m)}. "
        + string.Join(" ", AttributionSteps.Select(step =>
            $"{step.Label}: {FormatAmount(step.Value)}; running total {FormatAmount(step.End)}."))
        + $" Total change: {FormatAmount(Model?.TotalChange)}.";

    private static decimal NiceInterval(decimal range)
    {
        var approximate = (double)(range / 4m);
        if (approximate <= 0d)
            return range;

        var magnitude = Math.Pow(10d, Math.Floor(Math.Log10(approximate)));
        var fraction = approximate / magnitude;
        var multiplier = fraction < 1.5d ? 1d : fraction < 3.5d ? 2d : fraction < 7.5d ? 5d : 10d;
        var interval = multiplier * magnitude;
        var decimalInterval = interval > (double)decimal.MaxValue ? range : (decimal)interval;
        return decimalInterval > 0m ? decimalInterval : range;
    }

    private static IReadOnlyList<decimal> ChartTicks(ChartAxisScale scale)
    {
        var ticks = new List<decimal>();
        for (var tick = scale.Minimum; tick <= scale.Maximum && ticks.Count < 12; tick += scale.Interval)
            ticks.Add(tick);

        return ticks;
    }

    private static decimal ChartPosition(decimal value, ChartAxisScale scale) =>
        scale.Maximum == scale.Minimum
            ? 50m
            : 100m * (value - scale.Minimum) / (scale.Maximum - scale.Minimum);

    private static string PositionStyle(decimal value, ChartAxisScale scale) =>
        $"bottom:{ChartPosition(value, scale).ToString("0.###", CultureInfo.InvariantCulture)}%";

    private static string ConnectorStyle(int index, int columnCount, decimal value, ChartAxisScale scale)
    {
        var columnWidth = 100m / columnCount;
        var edgeOffset = columnWidth * 0.77m;
        var left = (index * columnWidth) + edgeOffset;
        var width = columnWidth * 0.46m;
        var bottom = ChartPosition(value, scale);
        return $"left:{left.ToString("0.###", CultureInfo.InvariantCulture)}%;width:{width.ToString("0.###", CultureInfo.InvariantCulture)}%;bottom:{bottom.ToString("0.###", CultureInfo.InvariantCulture)}%";
    }

    private static string BarStyle(AttributionColumn column, ChartAxisScale scale)
    {
        var start = ChartPosition(column.Start, scale);
        var end = ChartPosition(column.End, scale);
        var bottom = Math.Min(start, end).ToString("0.###", CultureInfo.InvariantCulture);
        var height = Math.Abs(end - start).ToString("0.###", CultureInfo.InvariantCulture);
        return $"bottom:{bottom}%;height:{height}%";
    }

    private static string BarTone(AttributionColumn column) => column.IsTotal
        ? $"total {ValueTone(column.Value)}"
        : ValueTone(column.Value);

    private static string ValueTone(decimal value) => value > 0m ? "positive" : value < 0m ? "negative" : "zero";

    private static string FormatColumnValue(AttributionColumn column)
    {
        return column.IsTotal
            ? MoneyFormatter.FormatNumber(column.Value)
            : MoneyFormatter.FormatSigned(column.Value);
    }

    private static string FormatAxisValue(decimal value) => Math.Abs(value) >= 1000m
        ? $"{(value / 1000m).ToString("0.0", CultureInfo.CurrentCulture)}k"
        : value.ToString("0.##", CultureInfo.CurrentCulture);

    private string FormatAmount(decimal? value)
    {
        if (value is not decimal amount)
            return "—";

        return MoneyFormatter.FormatSigned(amount, Currency.ShortName);
    }

    private sealed record AttributionStep(string Label, decimal Value, decimal Start, decimal End);

    private sealed record AttributionColumn(string Label, decimal Value, decimal Start, decimal End, bool IsTotal);

    private sealed record ChartAxisScale(decimal Minimum, decimal Maximum, decimal Interval);
}