using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Dtos;

public static class InvestmentTransactionMappingExtensions
{
    public static InvestmentTransactionDto ToDto(this InvestmentTransaction transaction) => new(
        transaction.EntryId,
        transaction.UserId,
        transaction.AccountId,
        transaction.AssetListingId,
        transaction.Type,
        transaction.Quantity,
        transaction.UnitPrice,
        transaction.Currency,
        transaction.TradeDate,
        transaction.Fee,
        transaction.Description.Length == 0 ? null : transaction.Description,
        transaction.AssetListing?.Ticker ?? string.Empty,
        transaction.AssetListing?.ExchangeName ?? string.Empty,
        transaction.AssetListing?.Asset?.Type ?? AssetType.Other,
        transaction.AssetListing?.HasEnabledSymbol ?? true,
        transaction.Labels.ToList());

    /// <summary>Build a new entity from an add request after its instrument source was resolved.</summary>
    public static InvestmentTransaction ToEntity(this AddInvestmentTransactionRequest request, long userId, long assetListingId) => new()
    {
        UserId = userId,
        AccountId = request.AccountId,
        AssetListingId = assetListingId,
        Type = request.Type,
        Quantity = request.Quantity,
        UnitPrice = request.UnitPrice,
        Currency = request.Currency,
        TradeDate = request.TradeDate,
        Fee = request.Fee,
        Description = request.Notes ?? string.Empty,
        Labels = (request.LabelIds ?? []).Distinct().Select(id => new FinanceManager.Domain.FinancialAccounts.Shared.Entities.FinancialLabel { Id = id, Name = string.Empty }).ToList()
    };

    /// <summary>Map an update request onto the editable fields the repository copies by id.</summary>
    public static InvestmentTransaction ToEntity(this UpdateInvestmentTransactionRequest request) => new()
    {
        EntryId = request.Id,
        AccountId = request.AccountId,
        AssetListingId = request.AssetListingId,
        Type = request.Type,
        Quantity = request.Quantity,
        UnitPrice = request.UnitPrice,
        Currency = request.Currency,
        TradeDate = request.TradeDate,
        Fee = request.Fee,
        Description = request.Notes ?? string.Empty,
        Labels = (request.LabelIds ?? []).Distinct().Select(id => new FinanceManager.Domain.FinancialAccounts.Shared.Entities.FinancialLabel { Id = id, Name = string.Empty }).ToList()
    };
}