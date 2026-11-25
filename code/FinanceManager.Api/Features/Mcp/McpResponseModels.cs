using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Transactions.Bonds.Dtos;
using FinanceManager.Domain.Transactions.Cash.Dtos;
using FinanceManager.Domain.Transactions.Investments.Entities;

namespace FinanceManager.Api.Features.Mcp;

/// <summary>One transaction of the authenticated user. <c>Kind</c> is "cash", "bond" or "investment"; ids are unique per kind.</summary>
public sealed record McpTransaction(
    long TransactionId,
    string Kind,
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
    long AssetListingId,
    string Ticker,
    string Exchange,
    string TradingCurrency,
    decimal Quantity,
    decimal Value);

public sealed record McpInvestmentPortfolio(
    DateOnly AsOfDate,
    string ValuationCurrency,
    decimal TotalValue,
    IReadOnlyList<McpInvestmentPosition> Positions);

public sealed record McpCurrency(int Id, string Code, string Symbol);
public sealed record McpLabelClassification(string Kind, string Value);
public sealed record McpFinancialLabel(int Id, string Name, IReadOnlyList<McpLabelClassification> Classifications);
public sealed record McpReferenceData(IReadOnlyList<McpCurrency> Currencies, IReadOnlyList<McpFinancialLabel> Labels);

internal static class McpResponseMapper
{
    // The cash counterparty (ContractorDetails) and an investment trade's private note are never exposed or matched.
    public static McpTransaction Transaction(CashLogEntry entry) => new(
        entry.EntryId,
        "cash",
        DateOnly.FromDateTime(entry.PostingDate),
        entry.ValueChange,
        entry.Description ?? string.Empty,
        LabelNames(entry.Labels),
        entry.Currency);

    public static McpTransaction Transaction(BondLogEntry entry) => new(
        entry.EntryId,
        "bond",
        DateOnly.FromDateTime(entry.PostingDate),
        entry.ValueChange,
        entry.BondName ?? "Bond",
        LabelNames(entry.Labels),
        entry.Currency);

    public static McpTransaction Transaction(InvestmentTransaction transaction) => new(
        transaction.EntryId,
        "investment",
        transaction.TradeDate,
        transaction.SignedQuantity * transaction.UnitPrice,
        $"{transaction.Type} {transaction.AssetListing?.Ticker}".TrimEnd(),
        LabelNames(transaction.Labels),
        transaction.Currency,
        transaction.Quantity,
        transaction.UnitPrice);

    private static string[] LabelNames(IEnumerable<FinancialLabel> labels) =>
        [.. labels.Select(label => label.Name).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];

    public static McpInvestmentPosition InvestmentPosition(
        AssetListing listing,
        decimal quantity,
        decimal value) => new(
            listing.Id,
            listing.Ticker,
            listing.ExchangeName,
            listing.TradingCurrency ?? string.Empty,
            quantity,
            value);

    public static McpCurrency Currency(Currency currency) =>
        new(currency.Id, currency.ShortName, currency.Symbol);

    public static McpFinancialLabel Label(FinancialLabel label) => new(
        label.Id,
        label.Name,
        [.. label.Classifications
            .OrderBy(classification => classification.Kind, StringComparer.Ordinal)
            .Select(classification => new McpLabelClassification(classification.Kind, classification.Value))]);
}