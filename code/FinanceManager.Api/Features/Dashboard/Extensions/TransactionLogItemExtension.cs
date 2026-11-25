using FinanceManager.Application.Transactions.Investments;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Cash.Dtos;
using FinanceManager.Domain.Transactions.Cash.Entities;

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
            e.EntryId, e.PostingDate, e.ValueChange, e.Description, e.BondDetailsId, e.Currency, e.BondName, [.. e.Labels.Select(label => label.ToLabelDto())]),
        InvestmentTransactionLogItem investment => new InvestmentTransactionLogItemDto(investment.Transaction.ToDto()),
        _ => throw new NotSupportedException($"Transaction log item {item.GetType().Name} is not supported."),
    };

    /// <summary>The log item for a cash history row.</summary>
    public static CurrencyTransactionLogItemDto ToLogItemDto(this CashLogEntry entry) => new(
        entry.EntryId, entry.PostingDate, entry.ValueChange, entry.Description, entry.CurrencyId, entry.Currency, entry.ContractorDetails,
        [.. entry.Labels.Select(label => label.ToLabelDto())]);

    /// <summary>The log item for a persisted cash entry (labels loaded) and its currency code.</summary>
    public static CurrencyTransactionLogItemDto ToLogItemDto(this CashEntry entry, string? currency) => new(
        entry.EntryId, entry.PostingDate, entry.ValueChange, entry.Description, entry.CurrencyId, currency, entry.ContractorDetails,
        [.. entry.Labels.Select(label => label.ToLabelDto())]);

    /// <summary>The log item for a persisted bond entry (labels loaded) and its definition's currency code and name.</summary>
    public static BondTransactionLogItemDto ToLogItemDto(this BondEntry entry, string? currency, string? bondName) => new(
        entry.EntryId, entry.PostingDate, entry.ValueChange, entry.Description, entry.BondDetailsId, currency, bondName,
        [.. entry.Labels.Select(label => label.ToLabelDto())]);
}