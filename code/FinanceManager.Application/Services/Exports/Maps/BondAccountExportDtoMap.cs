using CsvHelper.Configuration;
using FinanceManager.Domain.Entities.Exports;

namespace FinanceManager.Application.Services.Exports.Maps;

public sealed class BondAccountExportDtoMap : ClassMap<BondAccountExportDto>
{
    public BondAccountExportDtoMap()
    {
        Map(x => x.Id).Index(0).Name("Id");
        Map(x => x.PostingDate).Index(1).Name("PostingDate").TypeConverterOption.Format("O");
        Map(x => x.Value).Index(2).Name("Value");
        Map(x => x.ValueChange).Index(3).Name("ValueChange");
        Map(x => x.BondDetailsId).Index(4).Name("BondDetailsId");
    }
}