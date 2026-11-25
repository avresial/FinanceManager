namespace FinanceManager.Domain.Portfolio.Dividends.Dtos;

public sealed record DividendPaymentPage(IReadOnlyList<DividendPaymentDto> Items, int TotalCount, int Page, int PageSize);