using FinanceManager.Components.Shared.Helpers;
using System.Globalization;

namespace FinanceManager.Tests.Unit.Components.Shared.Helpers;

[Trait("Category", "Unit")]
public class MoneyFormatterTests
{
    [Theory]
    [InlineData(27771, "PLN", "27,771.00 PLN")]
    [InlineData(-1000, "PLN", "-1,000.00 PLN")]
    [InlineData(0, "EUR", "0.00 EUR")]
    [InlineData(1234567.891, "USD", "1,234,567.89 USD")]
    [InlineData(21600, "PLN", "21,600.00 PLN")]
    [InlineData(0.005, "PLN", "0.01 PLN")]
    public void Format_GroupsThousandsAndAppendsCurrency(double amount, string currency, string expected) =>
        Assert.Equal(expected, MoneyFormatter.Format((decimal)amount, currency));

    [Fact]
    public void Format_WithoutCurrency_OmitsSuffix()
    {
        Assert.Equal("26,671.00", MoneyFormatter.Format(26671m));
        Assert.Equal("26,671.00", MoneyFormatter.Format(26671m, " "));
        Assert.Equal("26,671.00", MoneyFormatter.FormatNumber(26671m));
    }

    [Fact]
    public void Format_TinyNegative_DoesNotProduceNegativeZero() =>
        Assert.Equal("0.00 PLN", MoneyFormatter.Format(-0.001m, "PLN"));

    [Theory]
    [InlineData(5000, "+5,000.00 PLN")]
    [InlineData(-5000, "-5,000.00 PLN")]
    [InlineData(0, "0.00 PLN")]
    [InlineData(-0.001, "0.00 PLN")]
    public void FormatSigned_AddsPlusOnlyForPositiveAmounts(double amount, string expected) =>
        Assert.Equal(expected, MoneyFormatter.FormatSigned((decimal)amount, "PLN"));

    [Fact]
    public void Format_IgnoresCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
            Assert.Equal("18,026.81 PLN", MoneyFormatter.Format(18026.81m, "PLN"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}