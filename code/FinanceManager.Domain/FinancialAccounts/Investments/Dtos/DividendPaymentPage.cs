namespace FinanceManager.Domain.FinancialAccounts.Investments.Dtos;

public sealed record DividendPaymentPage(IReadOnlyList<DividendPaymentDto> Items, int TotalCount, int Page, int PageSize);