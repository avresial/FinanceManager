using FinanceManager.Contracts.Assets;
using FinanceManager.Contracts.Labels;
using FinanceManager.Contracts.Transactions.Investments;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Transactions.Investments.Dtos;
using FinanceManager.Domain.Transactions.Investments.Entities;

namespace FinanceManager.Application.Transactions.Investments;

public static class InvestmentTransactionMappingExtensions
{
    public static InvestmentTransactionDto ToDto(this InvestmentTransaction transaction) => new(
        transaction.EntryId,
        transaction.UserId,
        transaction.AssetListingId,
        (InvestmentTransactionTypeDto)transaction.Type,
        transaction.Quantity,
        transaction.UnitPrice,
        transaction.Currency,
        transaction.TradeDate,
        transaction.Fee,
        transaction.Description.Length == 0 ? null : transaction.Description,
        transaction.AssetListing?.Ticker ?? string.Empty,
        transaction.AssetListing?.ExchangeName ?? string.Empty,
        (AssetTypeDto)(transaction.AssetListing?.Asset?.Type ?? AssetType.Other),
        transaction.AssetListing?.HasEnabledSymbol ?? true,
        [.. transaction.Labels.Select(ToLabelDto)]);

    public static FinancialLabelDto ToLabelDto(this FinancialLabel label) => new()
    {
        Id = label.Id,
        Name = label.Name,
        Classifications = label.Classifications?.Select(item => new FinancialLabelClassificationDto
        {
            Id = item.Id,
            LabelId = item.LabelId,
            Kind = item.Kind,
            Value = item.Value
        }).ToList()!
    };

    /// <summary>Build a new entity from an add request after its instrument source was resolved.</summary>
    public static InvestmentTransaction ToEntity(this CreateInvestmentTradeRequest request, long userId, long assetListingId) => new()
    {
        UserId = userId,
        AssetListingId = assetListingId,
        Type = (InvestmentTransactionType)request.Type,
        Quantity = request.Quantity,
        UnitPrice = request.UnitPrice,
        Currency = request.Currency,
        TradeDate = request.TradeDate,
        Fee = request.Fee,
        Description = request.Notes ?? string.Empty,
        Labels = (request.LabelIds ?? []).Distinct().Select(id => new FinancialLabel { Id = id, Name = string.Empty }).ToList()
    };

    /// <summary>Map an owner update request onto the editable fields the repository copies by id. The owner and account link are not part of the request.</summary>
    public static InvestmentTransaction ToEntity(this UpdateInvestmentTradeRequest request, long id) => new()
    {
        EntryId = id,
        AssetListingId = request.AssetListingId,
        Type = (InvestmentTransactionType)request.Type,
        Quantity = request.Quantity,
        UnitPrice = request.UnitPrice,
        Currency = request.Currency.Trim().ToUpperInvariant(),
        TradeDate = request.TradeDate,
        Fee = request.Fee,
        Description = request.Notes ?? string.Empty,
        Labels = (request.LabelIds ?? []).Distinct().Select(labelId => new FinancialLabel { Id = labelId, Name = string.Empty }).ToList()
    };

    public static InvestmentHoldingDto ToDto(this InvestmentHolding holding, string currency, decimal latestUnitPrice) => new(
        holding.AssetListingId, holding.Ticker, holding.ExchangeName, (AssetTypeDto)holding.AssetType, holding.Units, currency, latestUnitPrice);
}