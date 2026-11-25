using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Shared.Dtos;

namespace FinanceManager.Domain.Transactions.Bonds.Services;

public interface IBondUnrealizedGainLossCalculator
{
    /// <summary>
    /// Unrealized gain/loss of one owner-pooled bond position: its units are the pooled balance of the bond definition and
    /// interest accrues from the latest pooled entry. Returns <c>null</c> when nothing is held.
    /// </summary>
    Task<UnrealizedGainLossInstrumentResult?> CalculateAsync(OwnerLedgerPosition position, BondDetails? details, Currency currency, DateTime asOfDate);
}