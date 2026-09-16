namespace FinanceManager.Domain.MoneyFlow.Entities;

/// <summary>Supported forecast horizons exposed by the first version of the feature.</summary>
public static class CashFlowForecastHorizons
{
    public const int ThirtyDays = 30;
    public const int SixtyDays = 60;
    public const int NinetyDays = 90;

    public static IReadOnlyList<int> All { get; } = [ThirtyDays, SixtyDays, NinetyDays];

    public static bool IsSupported(int days) => days is ThirtyDays or SixtyDays or NinetyDays;
}