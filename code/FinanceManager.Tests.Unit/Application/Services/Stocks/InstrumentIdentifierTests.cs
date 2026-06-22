using FinanceManager.Application.FinancialAccounts.Stock.Resolution;

namespace FinanceManager.Tests.Unit.Application.Services.Stocks;

[Collection("Application")]
[Trait("Category", "Unit")]
public class InstrumentIdentifierTests
{
    [Theory]
    [InlineData("US0378331005")] // Apple
    [InlineData("IE00B5BMR087")] // iShares Core S&P 500 ETF
    [InlineData("US5949181045")] // Microsoft
    [InlineData("GB0002634946")] // BAE Systems
    [InlineData("DE000BAY0017")] // Bayer
    public void IsIsin_WithValidIsin_ReturnsTrue(string input)
    {
        Assert.True(InstrumentIdentifier.IsIsin(input));
    }

    [Fact]
    public void IsIsin_IsCaseInsensitiveAndTrims()
    {
        Assert.True(InstrumentIdentifier.IsIsin("  ie00b5bmr087 "));
    }

    [Theory]
    [InlineData("IE00B5BMR088")] // valid format, wrong check digit
    [InlineData("US0378331006")] // valid format, wrong check digit
    public void IsIsin_WithBadChecksum_ReturnsFalse(string input)
    {
        Assert.False(InstrumentIdentifier.IsIsin(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AAPL")]              // too short
    [InlineData("CSPX.UK")]           // broker ticker
    [InlineData("US037833100")]       // 11 chars
    [InlineData("US03783310055")]     // 13 chars
    [InlineData("1S0378331005")]      // first char not a letter
    [InlineData("U10378331005")]      // second char not a letter
    [InlineData("US037833100X")]      // final char not a digit
    public void IsIsin_WithInvalidInput_ReturnsFalse(string? input)
    {
        Assert.False(InstrumentIdentifier.IsIsin(input));
    }
}