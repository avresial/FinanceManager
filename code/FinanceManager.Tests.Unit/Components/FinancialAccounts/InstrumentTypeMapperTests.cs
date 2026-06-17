using FinanceManager.Components.Components.Features.FinancialAccounts.StockAccountComponents.Crud;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Tests.Unit.Components.FinancialAccounts;

[Trait("Category", "Unit")]
public class InstrumentTypeMapperTests
{
    [Theory]
    [InlineData("Equity", InvestmentType.Stock)]
    [InlineData("Common Stock", InvestmentType.Stock)]
    [InlineData("ETF", InvestmentType.Stock)]
    [InlineData("Mutual Fund", InvestmentType.Stock)]
    [InlineData("Bond", InvestmentType.Bond)]
    [InlineData("Treasury Note", InvestmentType.Bond)]
    [InlineData("Crypto", InvestmentType.Crypto)]
    [InlineData("Commodity", InvestmentType.Commodities)]
    [InlineData("REIT", InvestmentType.Property)]
    [InlineData("Real Estate", InvestmentType.Property)]
    [InlineData("Cash", InvestmentType.Cash)]
    public void MapToInvestmentType_MapsKnownTypes(string type, InvestmentType expected)
    {
        Assert.Equal(expected, InstrumentTypeMapper.MapToInvestmentType(type));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("something-unknown")]
    public void MapToInvestmentType_DefaultsToStock_WhenNullEmptyOrUnknown(string? type)
    {
        Assert.Equal(InvestmentType.Stock, InstrumentTypeMapper.MapToInvestmentType(type));
    }

    [Fact]
    public void MapToInvestmentType_IsCaseInsensitive()
    {
        Assert.Equal(InvestmentType.Bond, InstrumentTypeMapper.MapToInvestmentType("CORPORATE BOND"));
    }
}