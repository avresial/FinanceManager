using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;

namespace FinanceManager.Components.Features.FinancialAccounts.Models;

/// <summary>
/// The investment account-details trade list as rendered: the trades, the server-priced valuations
/// that decide what each row leads with, and the identity needed to rebuild the page header.
/// Content equality between a stored snapshot and a fresh API response is evaluated on this model,
/// so snapshot metadata can never look like a change.
/// </summary>
/// <remarks>
/// The currency is part of the rendered content, not just of the key: valuations are expressed in
/// it, so a preference changed since the snapshot was written must count as a change and repaint.
/// </remarks>
public sealed record InvestmentAccountDetailsModel(
    int UserId,
    int AccountId,
    string Name,
    Currency Currency,
    List<InvestmentTransactionDto> Transactions,
    List<InvestmentTransactionValuationDto> Valuations);