using CsvHelper.Configuration;
using FinanceManager.Domain.Entities.Exports;

namespace FinanceManager.Application.Services.Exports.Maps;

public sealed class BondAccountExportDtoMap : ClassMap<BondAccountExportDto>
{
    public BondAccountExportDtoMap()
    {
        Map(x => x.PostingDate).Index(0).Name("PostingDate").TypeConverterOption.Format("O");
        Map(x => x.ValueChange).Index(1).Name("ValueChange");
        Map(x => x.BondDetailsId).Index(2).Name("BondDetailsId");
    }
}