using FinanceManager.Application.Transactions.Investments;
using FinanceManager.Contracts.Transactions.Investments;
using FinanceManager.Domain.Transactions.Investments.Entities;

namespace FinanceManager.Tests.Unit.Domain.Transactions.Investments;

/// <summary>Investment notes are stored as the shared non-null Description but stay <c>Notes</c> (nullable) in public payloads.</summary>
[Collection("Domain")]
[Trait("Category", "Unit")]
public class InvestmentTransactionNotesMappingTests
{
    private static readonly DateOnly _tradeDate = new(2026, 5, 17);

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("Monthly ETF contribution", "Monthly ETF contribution")]
    public void AddRequest_MapsNotesToDescription_WithNullBecomingEmpty(string? notes, string expectedDescription)
    {
        var request = new CreateInvestmentTradeRequest(2, InvestmentTransactionTypeDto.Buy, 1m, 10m, "USD", _tradeDate, Notes: notes);

        var entity = request.ToEntity(userId: 7, assetListingId: 2);

        Assert.Equal(expectedDescription, entity.Description);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("Tech trim", "Tech trim")]
    public void UpdateRequest_MapsNotesToDescription_WithNullBecomingEmpty(string? notes, string expectedDescription)
    {
        var request = new UpdateInvestmentTradeRequest(2, InvestmentTransactionTypeDto.Sell, 1m, 10m, "USD", _tradeDate, Notes: notes);

        var entity = request.ToEntity(9);

        Assert.Equal(expectedDescription, entity.Description);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("Rebalancing", "Rebalancing")]
    public void ToDto_MapsEmptyDescriptionToNullNotes(string description, string? expectedNotes)
    {
        var transaction = new InvestmentTransaction
        {
            EntryId = 9,
            AccountId = 1,
            UserId = 7,
            AssetListingId = 2,
            Type = InvestmentTransactionType.Buy,
            Quantity = 1m,
            UnitPrice = 10m,
            Currency = "USD",
            TradeDate = _tradeDate,
            Description = description
        };

        var dto = transaction.ToDto();

        Assert.Equal(expectedNotes, dto.Notes);
    }
}