using FinanceManager.Components.Shared.Models;
using FinanceManager.Domain.Dashboard.Dtos;

namespace FinanceManager.Components.Features.Dashboard.Models;

public sealed class TransactionLogSnapshot : SnapshotBase
{
    public int UserId { get; set; }
    public int Count { get; set; }
    public List<TransactionLogEntryDto> Data { get; set; } = [];
}