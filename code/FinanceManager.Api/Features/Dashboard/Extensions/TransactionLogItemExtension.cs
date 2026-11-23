using FinanceManager.Api.Features.FinancialAccounts.Bond.Extensions;
using FinanceManager.Api.Features.FinancialAccounts.Currencies.Extensions;
using FinanceManager.Application.FinancialAccounts.Investments.Transactions;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Contracts.FinancialAccounts.Shared;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Api.Features.Dashboard.Extensions;

public static class TransactionLogItemExtension
{
    public static TransactionLogItemsPageDto ToDto(this TransactionLogItemsPage page) => new(
        [.. page.Items.Select(ToDto)],
        page.NextCursor is TransactionLogCursor cursor ? new(cursor.Date, (TransactionKindDto)cursor.Kind, cursor.EntryId) : null);

    public static TransactionLogItemDto ToDto(this TransactionLogItem item) => item switch
    {
        CurrencyTransactionLogItem { Entry: var e } => new CurrencyTransactionLogItemDto(
            e.LegacyAccountId, e.EntryId, e.PostingDate, e.ValueChange, e.Description, e.CurrencyId, e.Currency, e.ContractorDetails, [.. e.Labels.Select(label => label.ToLabelDto())]),
        BondTransactionLogItem { Entry: var e } => new BondTransactionLogItemDto(
            e.LegacyAccountId, e.EntryId, e.PostingDate, e.ValueChange, e.Description, e.BondDetailsId, e.Currency, e.BondName, [.. e.Labels.Select(label => label.ToLabelDto())]),
        InvestmentTransactionLogItem investment => new InvestmentTransactionLogItemDto(investment.LegacyAccountId, investment.Transaction.ToDto()),
        _ => throw new NotSupportedException($"Transaction log item {item.GetType().Name} is not supported."),
    };
}