using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;

namespace FinanceManager.Components.Features.FinancialAccounts.Models;

/// <summary>
/// Persisted snapshot of an investment account's trade list so the details page can paint its
/// last-rendered trades — and the valuations that decide what each row leads with — instantly on
/// re-navigation, then reconcile against a fresh API response. Chart data, holdings and the
/// appreciation figures are snapshotted separately, per selected range, by AccountChartSnapshotStore.
/// </summary>
/// <remarks>
/// Trades and valuations are stored together because they render as one surface: a row shows
/// gain/loss when it has been priced and its cash impact when it has not, so painting the trades
/// without their valuations would flip every amount on screen a moment later.
/// </remarks>
public sealed class InvestmentAccountDetailsSnapshot : SnapshotBase
{
    public int UserId { get; set; }
    public int AccountId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Currency the valuations are expressed in, and the one the header renders.</summary>
    public Currency Currency { get; set; } = new();

    public List<InvestmentTransactionDto> Transactions { get; set; } = [];
    public List<InvestmentTransactionValuationDto> Valuations { get; set; } = [];
}