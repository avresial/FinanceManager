using FinanceManager.Domain.FinancialAccounts.Investments.ValueObjects;
using System.Globalization;

namespace FinanceManager.Tests.Unit.Domain.FinancialAccounts.Investments;

public class InvestmentHistoryCursorTests
{
    [Fact]
    public void Create_RoundTripsEncodedCursor()
    {
        var expected = InvestmentHistoryCursor.Create(new DateOnly(2026, 9, 12), long.MaxValue);

        Assert.True(InvestmentHistoryCursor.TryCreate(expected.ToString(), out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ToStringAndTryCreate_UseInvariantFormat()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
            var cursor = InvestmentHistoryCursor.Create(new DateOnly(2026, 9, 12), 42);

            Assert.Equal("MjAyNi0wOS0xMjo0Mg==", cursor.ToString());
            Assert.True(InvestmentHistoryCursor.TryCreate("2026-09-12:42", out var parsed));
            Assert.Equal(cursor, parsed);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData("2026-09-12:42")]
    [InlineData("2026-09-12|42")]
    [InlineData("2026-09-12_42")]
    [InlineData(" 2026-09-12:42 ")]
    public void TryCreate_AcceptsExistingPlainTextFormats(string value)
    {
        Assert.True(InvestmentHistoryCursor.TryCreate(value, out var cursor));
        Assert.Equal(InvestmentHistoryCursor.Create(new DateOnly(2026, 9, 12), 42), cursor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-a-valid-cursor")]
    [InlineData("2026-09-12:0")]
    [InlineData("2026-09-12:-1")]
    [InlineData("2026-09-12:9223372036854775808")]
    [InlineData("2026-02-30:42")]
    [InlineData("2026-09-12:")]
    [InlineData("2026-09-12:42:43")]
    [InlineData("MjAyNi0wOS0xMjow")]
    public void TryCreate_RejectsInvalidCursors(string? value)
    {
        Assert.False(InvestmentHistoryCursor.TryCreate(value, out var cursor));
        Assert.Null(cursor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_RejectsNonPositiveIds(long id) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => InvestmentHistoryCursor.Create(new DateOnly(2026, 9, 12), id));
}