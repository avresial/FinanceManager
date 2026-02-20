using CsvHelper.Configuration;
using FinanceManager.Domain.Entities.Exports;

namespace FinanceManager.Application.Services.Exports.Maps;

public sealed class CurrencyAccountExportDtoMap : ClassMap<CurrencyAccountExportDto>
{
    public CurrencyAccountExportDtoMap()
    {
        Map(x => x.Id).Index(0).Name("Id");
        Map(x => x.PostingDate).Index(1).Name("PostingDate").TypeConverterOption.Format("O");
        Map(x => x.ValueChange).Index(2).Name("ValueChange");
        Map(x => x.ContractorDetails).Index(3).Name("ContractorDetails");
        Map(x => x.Description).Index(4).Name("Description");
        Map(x => x.Labels).Index(5).Name("Labels");
    }
}