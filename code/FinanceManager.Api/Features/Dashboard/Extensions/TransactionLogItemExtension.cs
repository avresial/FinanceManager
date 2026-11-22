using FinanceManager.Api.Features.FinancialAccounts.Bond.Extensions;
using FinanceManager.Api.Features.FinancialAccounts.Currencies.Extensions;
using FinanceManager.Application.FinancialAccounts.Investments.Transactions;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Contracts.FinancialAccounts.Shared;
using FinanceManager.Domain.Dashboard.Dtos;

namespace FinanceManager.Api.Features.Dashboard.Extensions;

public static class TransactionLogItemExtension
{
    public static TransactionLogItemsPageDto ToDto(this TransactionLogItemsPage page) => new(
        [.. page.Items.Select(ToDto)],
        page.NextCursor is TransactionLogCursor cursor ? new(cursor.Date, (AccountTypeDto)cursor.AccountType, cursor.EntryId) : null);

    public static TransactionLogItemDto ToDto(this TransactionLogItem item) => item switch
    {
        CurrencyTransactionLogItem currency => new CurrencyTransactionLogItemDto(
            currency.AccountId, currency.AccountName, currency.CurrencyId, currency.Currency, currency.Entry.ToDto()),
        BondTransactionLogItem bond => new BondTransactionLogItemDto(bond.AccountId, bond.AccountName, bond.Entry.ToDto()),
        InvestmentTransactionLogItem investment => new InvestmentTransactionLogItemDto(
            investment.AccountId, investment.AccountName, investment.Transaction.ToDto()),
        _ => throw new NotSupportedException($"Transaction log item {item.GetType().Name} is not supported."),
    };
}