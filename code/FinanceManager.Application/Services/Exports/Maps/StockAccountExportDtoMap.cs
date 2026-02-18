using CsvHelper.Configuration;
using FinanceManager.Domain.Entities.Exports;

namespace FinanceManager.Application.Services.Exports.Maps;

public sealed class StockAccountExportDtoMap : ClassMap<StockAccountExportDto>
{
    public StockAccountExportDtoMap()
    {
        Map(x => x.PostingDate).Index(0).Name("PostingDate").TypeConverterOption.Format("O");
        Map(x => x.ValueChange).Index(1).Name("ValueChange");
        Map(x => x.SharesCount).Index(2).Name("SharesCount");
        Map(x => x.Price).Index(3).Name("Price");
        Map(x => x.Ticker).Index(4).Name("Ticker");
        Map(x => x.InvestmentType).Index(5).Name("InvestmentType");
    }
}