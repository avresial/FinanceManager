using CsvHelper.Configuration;
using FinanceManager.Domain.Transactions.Cash.Exports;

namespace FinanceManager.Application.Transactions.Export;

public sealed class CashExportDtoMap : ClassMap<CashExportDto>
{
    public CashExportDtoMap()
    {
        var index = 0;
        Map(x => x.Id).Index(index++).Name("Id");
        Map(x => x.PostingDate).Index(index++).Name("PostingDate").TypeConverterOption.Format("yyyy-MM-ddTHH:mm:ssZ");
        Map(x => x.ValueChange).Index(index++).Name("ValueChange");
        Map(x => x.ContractorDetails).Index(index++).Name("ContractorDetails");
        Map(x => x.Description).Index(index).Name("Description");
    }
}