namespace FinanceManager.Domain.FinancialAccounts.Investments.Dtos;

/// <summary>
/// Bounded page of investment transactions for an account with continuation metadata.
/// </summary>
public record InvestmentTransactionHistoryPageDto(
    IReadOnlyList<InvestmentTransactionDto> Items,
    bool HasMore,
    string? NextCursor = null);