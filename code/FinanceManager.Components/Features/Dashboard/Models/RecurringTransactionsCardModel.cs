using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>
/// What the Recurring transactions card renders. Carries no snapshot metadata, so content equality
/// over this model decides whether a refresh actually changed anything on screen.
/// </summary>
/// <param name="Data">Detected recurring transactions, newest grouping first.</param>
/// <param name="TotalMonthlySpend">Sum of <paramref name="Data"/> values, rounded to two decimals.</param>
public sealed record RecurringTransactionsCardModel(
    List<RecurringTransactionResult> Data,
    decimal TotalMonthlySpend);