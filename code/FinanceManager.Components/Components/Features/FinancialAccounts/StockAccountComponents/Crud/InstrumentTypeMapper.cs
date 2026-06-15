using FinanceManager.Domain.Enums;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.StockAccountComponents.Crud;

/// <summary>
/// Maps the free-form instrument type string returned by instrument resolution
/// (e.g. Alpha Vantage / OpenFIGI: "Equity", "ETF", "Bond") onto the app's <see cref="InvestmentType"/>.
/// Defaults to <see cref="InvestmentType.Stock"/> because resolution is only used from stock accounts.
/// </summary>
public static class InstrumentTypeMapper
{
    public static InvestmentType MapToInvestmentType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return InvestmentType.Stock;

        var normalized = type.Trim().ToLowerInvariant();

        if (normalized.Contains("bond") || normalized.Contains("note") || normalized.Contains("bill") || normalized.Contains("treasury"))
            return InvestmentType.Bond;
        if (normalized.Contains("crypto"))
            return InvestmentType.Crypto;
        if (normalized.Contains("commodit"))
            return InvestmentType.Commodities;
        if (normalized.Contains("reit") || normalized.Contains("real estate") || normalized.Contains("property"))
            return InvestmentType.Property;
        if (normalized.Contains("cash") || normalized.Contains("currency"))
            return InvestmentType.Cash;
        if (normalized.Contains("equity") || normalized.Contains("stock") || normalized.Contains("share")
            || normalized.Contains("etf") || normalized.Contains("fund"))
            return InvestmentType.Stock;

        return InvestmentType.Stock;
    }
}