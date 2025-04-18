﻿using FinanceManager.Domain.Enums;

namespace FinanceManager.Infrastructure.Dtos;

public class BankAccountDto : FinancialAccountBaseDto
{
    public DateTime? OlderThanLoadedEntry { get; set; }
    public DateTime? YoungerThanLoadedEntry { get; set; }
    public AccountType AccountType { get; set; }
    public IEnumerable<BankAccountEntryDto> Entries { get; set; } = [];
};
