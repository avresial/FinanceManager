using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace FinanceManager.Domain.FinancialAccounts.Investments.ValueObjects;

public sealed record InvestmentHistoryCursor
{
    public DateOnly TradeDate { get; }
    public long Id { get; }

    // Keep construction private so every cursor has a valid transaction ID.
    private InvestmentHistoryCursor(DateOnly tradeDate, long id)
    {
        TradeDate = tradeDate;
        Id = id;
    }

    public static InvestmentHistoryCursor Create(DateOnly tradeDate, long id)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        return new InvestmentHistoryCursor(tradeDate, id);
    }

    public static bool TryCreate(string? value, [NotNullWhen(true)] out InvestmentHistoryCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var raw = value.Trim();
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(raw));
            if (TryCreateDelimited(decoded, out cursor)) return true;
        }
        catch (FormatException)
        {
            // Existing callers may supply a plain-text cursor instead of Base64.
        }

        return TryCreateDelimited(raw, out cursor);
    }

    public override string ToString() =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{TradeDate:yyyy-MM-dd}:{Id}"));

    private static bool TryCreateDelimited(string text, [NotNullWhen(true)] out InvestmentHistoryCursor? cursor)
    {
        cursor = null;
        var parts = text.Split([':', '|', '_'], 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !DateOnly.TryParse(parts[0], out var tradeDate) ||
            !long.TryParse(parts[1], out var id) || id <= 0) return false;

        cursor = Create(tradeDate, id);
        return true;
    }
}