using FinanceManager.Domain.Dashboard.Dtos;

namespace FinanceManager.Components.Features.Dashboard.Models;

public record TransactionHistoryPage(IReadOnlyList<TransactionHistoryItem> Items, TransactionLogCursor? NextCursor);