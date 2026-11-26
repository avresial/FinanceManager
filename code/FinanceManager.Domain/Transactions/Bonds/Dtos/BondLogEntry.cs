using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Domain.Transactions.Bonds.Dtos;

public record BondLogEntry(long EntryId, DateTime PostingDate, decimal ValueChange,
    string Description, int BondDetailsId, string? Currency, string? BondName, IReadOnlyList<FinancialLabel> Labels);