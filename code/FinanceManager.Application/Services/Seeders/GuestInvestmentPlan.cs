namespace FinanceManager.Application.Services.Seeders;

// Shared schedule for the guest demo's recurring investment so the cash and stock seeders stay in sync:
// on day 3 of each month the cash account spends MonthlyAmount on "Investment" and the stock account buys
// the matching value of shares at that day's price, so the holding's cost basis tracks the cash outflow
// and only its market value drifts afterwards.
internal static class GuestInvestmentPlan
{
    public const decimal MonthlyAmount = 500m;
    public const int DayOfMonth = 3;

    public static bool IsInvestmentDay(DateTime date) => date.Day == DayOfMonth;
}
