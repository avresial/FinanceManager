using FinanceManager.Components.Features.FinancialAccounts.Components.Shared;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Services;

[Trait("Category", "Unit")]
public class InvestmentTransactionHistoryFilterTests
{
    private static readonly DateTime _start = new(2024, 1, 1);
    private static readonly DateTime _end = new(2024, 1, 31);

    [Fact]
    public void BuildQuery_MapsIncomeToSell_AndTrimsSearch()
    {
        var query = InvestmentTransactionHistoryFilter.BuildQuery(_start, _end, AccountHistoryToolbar.TxFilter.Income, "  vwce ");

        Assert.Equal(new DateOnly(2024, 1, 1), query.StartDate);
        Assert.Equal(new DateOnly(2024, 1, 31), query.EndDate);
        Assert.Equal(InvestmentTransactionType.Sell, query.Type);
        Assert.Equal("vwce", query.Search);
        Assert.False(InvestmentTransactionHistoryFilter.IsUnfiltered(query));
    }

    [Fact]
    public void BuildQuery_BlankSearch_IsNull()
    {
        var query = InvestmentTransactionHistoryFilter.BuildQuery(_start, _end, null, "   ");

        Assert.Null(query.Type);
        Assert.Null(query.Search);
    }

    [Fact]
    public void Unfiltered_IsUnfiltered() =>
        Assert.True(InvestmentTransactionHistoryFilter.IsUnfiltered(InvestmentTransactionHistoryFilter.Unfiltered));

    [Fact]
    public void Apply_FiltersByRangeAndDirection_NewestFirst()
    {
        Tx[] transactions =
        [
            new(1, InvestmentTransactionType.Buy, new DateOnly(2024, 1, 5)),
            new(2, InvestmentTransactionType.Sell, new DateOnly(2024, 1, 10)),
            new(3, InvestmentTransactionType.Buy, new DateOnly(2024, 1, 20)),
            new(4, InvestmentTransactionType.Buy, new DateOnly(2024, 2, 2)),
        ];

        var result = InvestmentTransactionHistoryFilter.Apply(
            transactions.Select(t => t.ToDto()), _start, _end, AccountHistoryToolbar.TxFilter.Expense, null,
            InvestmentTransactionHistoryFilter.Unfiltered);

        Assert.Equal([3L, 1L], result.Select(t => t.Id));
    }

    [Fact]
    public void Apply_SearchesLocally_OnlyWhenTheServerDidNotApplyTheSameSearch()
    {
        var dtos = new[]
        {
            new Tx(1, InvestmentTransactionType.Buy, new DateOnly(2024, 1, 5), "VWCE").ToDto(),
            new Tx(2, InvestmentTransactionType.Buy, new DateOnly(2024, 1, 6), "CSPX").ToDto(),
        };

        var local = InvestmentTransactionHistoryFilter.Apply(dtos, _start, _end, null, "vwce", InvestmentTransactionHistoryFilter.Unfiltered);
        var serverQuery = InvestmentTransactionHistoryFilter.BuildQuery(_start, _end, null, "VWCE");
        var server = InvestmentTransactionHistoryFilter.Apply(dtos, _start, _end, null, "vwce", serverQuery);

        Assert.Equal([1L], local.Select(t => t.Id));
        Assert.Equal(2, server.Count);
    }

    [Fact]
    public void CashImpact_SellIsNetOfFee_BuyIsNegativeIncludingFee()
    {
        var sell = new Tx(1, InvestmentTransactionType.Sell, new DateOnly(2024, 1, 5)).ToDto() with { Quantity = 2m, UnitPrice = 10m, Fee = 1m };
        var buy = sell with { Type = InvestmentTransactionType.Buy };

        Assert.Equal(19m, InvestmentTransactionHistoryFilter.CashImpact(sell));
        Assert.Equal(-21m, InvestmentTransactionHistoryFilter.CashImpact(buy));
    }

    private sealed record Tx(long Id, InvestmentTransactionType Type, DateOnly TradeDate, string Ticker = "T")
    {
        public InvestmentTransactionDto ToDto() =>
            new(Id, 1, 1, 10, Type, 1m, 1m, "USD", TradeDate, null, null, Ticker);
    }
}