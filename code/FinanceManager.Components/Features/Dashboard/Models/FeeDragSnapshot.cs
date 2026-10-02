using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;

namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>
/// Last rendered ETF fee drag analysis. <see cref="Analysis"/> holds only the facts the card renders
/// (no server projections or assumed rate), so the slider can never influence equality or storage.
/// </summary>
public sealed class FeeDragSnapshot : SnapshotBase
{
    public int UserId { get; set; }
    public int CurrencyId { get; set; }
    public DateTime AsOfDate { get; set; }
    public FeeDragAnalysisResult Analysis { get; set; } = new();
}