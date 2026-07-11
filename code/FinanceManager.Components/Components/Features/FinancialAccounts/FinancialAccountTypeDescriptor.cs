using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

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

        if (accountType == typeof(InvestmentAccount))
            return new(accountType, FinancialAccountKind.Stock, "Stock", "InvestmentAccount");

        if (accountType == typeof(BondAccount))
            return new(accountType, FinancialAccountKind.Bond, "Bond", "BondAccount");

        return null;
    }
}