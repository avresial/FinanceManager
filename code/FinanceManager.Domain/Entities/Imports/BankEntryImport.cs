using System;

namespace FinanceManager.Domain.Entities.Imports;

public record BankEntryImport(DateTime PostingDate, decimal ValueChange);
