using FinanceManager.Components.Shared.Models;

namespace FinanceManager.Components.Features.Dashboard.Models;

public sealed class InvestmentPaycheckSourceSnapshot : SnapshotBase
{
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public int SalaryMonths { get; set; }
    public DateTime AsOfDateUtc { get; set; }
    public InvestmentPaycheckSourceModel? Model { get; set; }
}
