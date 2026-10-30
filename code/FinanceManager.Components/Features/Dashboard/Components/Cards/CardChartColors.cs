using FinanceManager.Domain.Shared.Charting;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards;

internal static class CardChartColors
{
    public static readonly string Accent = ColorsProvider.GetColors().First();
    public const string Neutral = ColorsProvider.OtherColor;
}