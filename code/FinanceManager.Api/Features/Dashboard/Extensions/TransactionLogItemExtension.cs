using FinanceManager.Api.Features.FinancialAccounts.Bond.Extensions;
using FinanceManager.Api.Features.FinancialAccounts.Currencies.Extensions;
using FinanceManager.Application.FinancialAccounts.Investments.Transactions;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Transactions.Cash.Dtos;

namespace FinanceManager.Api.Features.Dashboard.Extensions;

public static class TransactionLogItemExtension
{
    public static TransactionLogItemsPageDto ToDto(this TransactionLogItemsPage page) => new(
        [.. page.Items.Select(ToDto)],
        page.NextCursor is TransactionLogCursor cursor ? new(cursor.Date, (TransactionKindDto)cursor.Kind, cursor.EntryId) : null);

    public static TransactionLogItemDto ToDto(this TransactionLogItem item) => item switch
    {
        CurrencyTransactionLogItem { Entry: var e } => e.ToLogItemDto(),
        BondTransactionLogItem { Entry: var e } => new BondTransactionLogItemDto(
            e.LegacyAccountId, e.EntryId, e.PostingDate, e.ValueChange, e.Description, e.BondDetailsId, e.Currency, e.BondName, [.. e.Labels.Select(label => label.ToLabelDto())]),
        InvestmentTransactionLogItem investment => new InvestmentTransactionLogItemDto(investment.LegacyAccountId, investment.Transaction.ToDto()),
        _ => throw new NotSupportedException($"Transaction log item {item.GetType().Name} is not supported."),
    };

    /// <summary>The log item for a cash history row.</summary>
    public static CurrencyTransactionLogItemDto ToLogItemDto(this CashLogEntry entry) => new(
        entry.LegacyAccountId, entry.EntryId, entry.PostingDate, entry.ValueChange, entry.Description, entry.CurrencyId, entry.Currency, entry.ContractorDetails,
        [.. entry.Labels.Select(label => label.ToLabelDto())]);

    /// <summary>The log item for a persisted cash entry (labels loaded) and its currency code.</summary>
    public static CurrencyTransactionLogItemDto ToLogItemDto(this CurrencyAccountEntry entry, string? currency) => new(
        entry.AccountId, entry.EntryId, entry.PostingDate, entry.ValueChange, entry.Description, entry.CurrencyId, currency, entry.ContractorDetails,
        [.. entry.Labels.Select(label => label.ToLabelDto())]);

    /// <summary>The log item for a persisted bond entry (labels loaded) and its definition's currency code and name.</summary>
    public static BondTransactionLogItemDto ToLogItemDto(this BondAccountEntry entry, string? currency, string? bondName) => new(
        entry.AccountId, entry.EntryId, entry.PostingDate, entry.ValueChange, entry.Description, entry.BondDetailsId, currency, bondName,
        [.. entry.Labels.Select(label => label.ToLabelDto())]);
}