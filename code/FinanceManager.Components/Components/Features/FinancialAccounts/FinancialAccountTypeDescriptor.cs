using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;

namespace FinanceManager.Components.Components.Features.FinancialAccounts;

public sealed record FinancialAccountTypeDescriptor(
    Type AccountType,
    FinancialAccountKind Kind,
    string Label,
    string ExportEndpointName)
{
    public string GetExportEndpoint(int accountId) => $"api/{ExportEndpointName}/export/{accountId}";

    public static FinancialAccountTypeDescriptor? FromType(Type? accountType)
    {
        if (accountType == typeof(CurrencyAccount))
            return new(accountType, FinancialAccountKind.Currency, "Currency", "CurrencyAccount");

        if (accountType == typeof(StockAccount))
            return new(accountType, FinancialAccountKind.Stock, "Stock", "StockAccount");

        if (accountType == typeof(BondAccount))
            return new(accountType, FinancialAccountKind.Bond, "Bond", "BondAccount");

        return null;
    }
}