using CsvHelper.Configuration;
using FinanceManager.Domain.Entities.Exports;

namespace FinanceManager.Application.Services.Exports.Maps;

public sealed class StockAccountExportDtoMap : ClassMap<StockAccountExportDto>
{
    public StockAccountExportDtoMap()
    {
        Map(x => x.Id).Index(0).Name("Id");
        Map(x => x.PostingDate).Index(1).Name("PostingDate").TypeConverterOption.Format("O");
        Map(x => x.ValueChange).Index(2).Name("ValueChange");
        Map(x => x.SharesCount).Index(3).Name("SharesCount");
        Map(x => x.Price).Index(4).Name("Price");
        Map(x => x.Ticker).Index(5).Name("Ticker");
        Map(x => x.InvestmentType).Index(6).Name("InvestmentType");
        Map(x => x.Labels).Index(7).Name("Labels");
    }
}