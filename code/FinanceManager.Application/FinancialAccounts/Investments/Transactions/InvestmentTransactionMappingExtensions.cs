using FinanceManager.Contracts.Assets;
using FinanceManager.Contracts.FinancialAccounts.Investments;
using FinanceManager.Contracts.FinancialAccounts.Shared;
using FinanceManager.Contracts.Transactions.Investments;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Application.FinancialAccounts.Investments.Transactions;

public static class InvestmentTransactionMappingExtensions
{
    public static InvestmentTransactionDto ToDto(this InvestmentTransaction transaction) => new(
        transaction.EntryId,
        transaction.UserId,
        transaction.AccountId,
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
    public static InvestmentTransaction ToEntity(this AddInvestmentTransactionRequest request, long userId, int? accountId, long assetListingId) => new()
    {
        UserId = userId,
        AccountId = accountId,
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

    /// <summary>Map an owner add request onto the shared add pipeline's request. The pipeline takes the account link separately, so none is set here.</summary>
    public static AddInvestmentTransactionRequest ToAddRequest(this CreateInvestmentTradeRequest request) => new(
        0, request.AssetListingId, request.Type, request.Quantity, request.UnitPrice, request.Currency, request.TradeDate,
        request.Fee, request.Notes, request.ExternalInstrumentResultId, request.LabelIds);

    /// <summary>Map an update request onto the editable fields the repository copies by id.</summary>
    public static InvestmentTransaction ToEntity(this UpdateInvestmentTransactionRequest request) => new()
    {
        EntryId = request.Id,
        AccountId = request.AccountId,
        AssetListingId = request.AssetListingId,
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
        Currency = request.Currency,
        TradeDate = request.TradeDate,
        Fee = request.Fee,
        Description = request.Notes ?? string.Empty,
        Labels = (request.LabelIds ?? []).Distinct().Select(labelId => new FinancialLabel { Id = labelId, Name = string.Empty }).ToList()
    };

    public static InvestmentHoldingDto ToDto(this InvestmentHolding holding) => new(
        holding.AssetListingId, holding.Ticker, holding.ExchangeName, (AssetTypeDto)holding.AssetType, holding.Units);
}