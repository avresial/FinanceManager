namespace FinanceManager.Application.Identity.Seeders;

// Single source of truth for the guest demo's correlated money flows so every seeder stays in sync:
// the pooled cash is the hub, and stock purchases, bond purchases and loan repayments flow out of it.
// Keeping the amounts and calendar days here lets the cash, stock and bond seeders book matching
// entries without depending on each other directly.
internal static class GuestInvestmentPlan
{
    // Stock: recurring monthly purchase of the demo ETF, funded by a matching cash outflow.
    public const decimal StockMonthlyAmount = 500m;
    public const int StockDayOfMonth = 3;

    // Bond: recurring monthly purchase of the demo treasury bond, funded by a matching cash outflow.
    // Kept a whole multiple of the bond unit value (100) so the cash spend and the seeded units line up exactly.
    public const decimal BondMonthlyAmount = 300m;
    public const int BondDayOfMonth = 5;

    // Loan: repaid in equal monthly instalments, a plain recurring cash outflow. There is no disbursement entry and no
    // loan account, so the demo's net worth is not inflated by borrowed money it never shows as a liability.
    public const decimal LoanMonthlyRepayment = 400m;
    public const int LoanRepaymentDayOfMonth = 6;

    public static bool IsStockInvestmentDay(DateTime date) => date.Day == StockDayOfMonth;
    public static bool IsBondInvestmentDay(DateTime date) => date.Day == BondDayOfMonth;
}