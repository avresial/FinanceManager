using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Api.Features.Mcp;

public sealed record McpFinancialAccount(int AccountId, string Name, string Type, string Category);

public sealed record McpTransaction(
    long TransactionId,
    int AccountId,
    string AccountName,
    string AccountType,
    DateOnly Date,
    decimal Amount,
    string Description,
    string[] Labels,
    string? Currency = null,
    decimal? Quantity = null,
    decimal? UnitPrice = null);

public sealed record McpTransactionPage(
    IReadOnlyList<McpTransaction> Transactions,
    int Offset,
    int Limit,
    bool HasMore);

public sealed record McpInvestmentPosition(
    int AccountId,
    string AccountName,
    long AssetListingId,
    string Ticker,
    string Exchange,
    string TradingCurrency,
    decimal Quantity);

public sealed record McpInvestmentAccountValue(int AccountId, string AccountName, decimal Value);

public sealed record McpInvestmentPortfolio(
    DateOnly AsOfDate,
    string ValuationCurrency,
    decimal TotalValue,
    IReadOnlyList<McpInvestmentAccountValue> Accounts,
    IReadOnlyList<McpInvestmentPosition> Positions);

public sealed record McpCurrency(int Id, string Code, string Symbol);
public sealed record McpLabelClassification(string Kind, string Value);
public sealed record McpFinancialLabel(int Id, string Name, IReadOnlyList<McpLabelClassification> Classifications);
public sealed record McpReferenceData(IReadOnlyList<McpCurrency> Currencies, IReadOnlyList<McpFinancialLabel> Labels);

internal static class McpResponseMapper
{
    public static McpFinancialAccount Account(CurrencyAccount account) =>
        new(account.AccountId, account.Name, "currency", account.AccountType.ToString());

    public static McpFinancialAccount Account(BondAccount account) =>
        new(account.AccountId, account.Name, "bond", AccountLabel.Bond.ToString());

    public static McpFinancialAccount Account(InvestmentAccount account) =>
        new(account.AccountId, account.Name, "investment", AccountLabel.Stock.ToString());

    public static McpTransaction Transaction(CurrencyAccountEntry entry, string accountName) => new(
        entry.EntryId,
        entry.AccountId,
        accountName,
        "currency",
        DateOnly.FromDateTime(entry.PostingDate),
        entry.ValueChange,
        entry.Description ?? string.Empty,
        [.. entry.Labels.Select(label => label.Name).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)]);

    public static McpTransaction Transaction(BondAccountEntry entry, string accountName, string bondName) => new(
        entry.EntryId,
        entry.AccountId,
        accountName,
        "bond",
        DateOnly.FromDateTime(entry.PostingDate),
        entry.ValueChange,
        bondName,
        [.. entry.Labels.Select(label => label.Name).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)]);

    public static McpTransaction Transaction(InvestmentTransaction transaction, string accountName) => new(
        transaction.Id,
        transaction.AccountId,
        accountName,
        "investment",
        transaction.TradeDate,
        transaction.SignedQuantity * transaction.UnitPrice,
        $"{transaction.Type} {transaction.AssetListing?.Ticker}".TrimEnd(),
        [],
        transaction.Currency,
        transaction.Quantity,
        transaction.UnitPrice);

    public static McpInvestmentPosition InvestmentPosition(
        int accountId,
        string accountName,
        AssetListing listing,
        decimal quantity) => new(
            accountId,
            accountName,
            listing.Id,
            listing.Ticker,
            listing.ExchangeName,
            listing.TradingCurrency,
            quantity);

    public static McpCurrency Currency(Currency currency) =>
        new(currency.Id, currency.ShortName, currency.Symbol);

    public static McpFinancialLabel Label(FinancialLabel label) => new(
        label.Id,
        label.Name,
        [.. label.Classifications
            .OrderBy(classification => classification.Kind, StringComparer.Ordinal)
            .Select(classification => new McpLabelClassification(classification.Kind, classification.Value))]);
}