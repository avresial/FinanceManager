using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

public sealed record RecurringTransactionsCardModel(
    List<RecurringTransactionResult> Data,
    decimal TotalMonthlySpend);