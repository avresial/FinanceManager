using FinanceManager.Application.FinancialAccounts.Stock.Seeders;

namespace FinanceManager.Application.Options;

public sealed class StockDetailsSeederOptions
{
    public const string SectionName = "StockDetailsSeeder";

    public bool Enabled { get; set; }
}