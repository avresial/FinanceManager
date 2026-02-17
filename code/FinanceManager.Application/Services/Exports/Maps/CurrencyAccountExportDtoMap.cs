using CsvHelper.Configuration;
using FinanceManager.Domain.Entities.Exports;

namespace FinanceManager.Application.Services.Exports.Maps;

public sealed class CurrencyAccountExportDtoMap : ClassMap<CurrencyAccountExportDto>
{
    public CurrencyAccountExportDtoMap()
    {
        Map(x => x.PostingDate).Index(0).Name("PostingDate").TypeConverterOption.Format("O");
        Map(x => x.ValueChange).Index(1).Name("ValueChange");
        Map(x => x.ContractorDetails).Index(2).Name("ContractorDetails");
        Map(x => x.Description).Index(3).Name("Description");
    }
}