using CsvHelper.Configuration;
using FinanceManager.Domain.Entities.Exports;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;

namespace FinanceManager.Application.FinancialAccounts.Stock.Export;

public sealed class StockAccountExportDtoMap : ClassMap<StockAccountExportDto>
{
    public StockAccountExportDtoMap()
    {
        Map(x => x.Id).Index(0).Name("Id");
        Map(x => x.PostingDate).Index(1).Name("PostingDate").TypeConverterOption.Format("yyyy-MM-ddTHH:mm:ssZ");
        Map(x => x.ValueChange).Index(2).Name("ValueChange");
        Map(x => x.SharesCount).Index(3).Name("Value");
        Map(x => x.Price).Index(4).Name("Price");
        Map(x => x.Ticker).Index(5).Name("Ticker");
        Map(x => x.InvestmentType).Index(6).Name("InvestmentType");
    }
}