namespace FinanceManager.Application.Services.Seeders;

// Single source of truth for the guest demo's correlated money flows so every account seeder stays in sync:
// the cash account is the hub, and loan proceeds flow into it while stock and bond purchases flow out of it.
// Keeping the amounts and calendar days here lets the cash, loan, stock, and bond seeders book matching
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

    // Loan: borrowed in full on the guest's first day (cash sees the matching inflow), then repaid in equal
    // monthly instalments that shrink the debt toward zero with a matching cash outflow.
    public const decimal LoanPrincipal = 24_000m;
    public const decimal LoanMonthlyRepayment = 400m;
    public const int LoanRepaymentDayOfMonth = 6;

    public static bool IsStockInvestmentDay(DateTime date) => date.Day == StockDayOfMonth;
    public static bool IsBondInvestmentDay(DateTime date) => date.Day == BondDayOfMonth;
    public static bool IsLoanRepaymentDay(DateTime date) => date.Day == LoanRepaymentDayOfMonth;
}