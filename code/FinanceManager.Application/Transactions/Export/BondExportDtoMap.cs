using CsvHelper.Configuration;
using FinanceManager.Domain.Transactions.Bonds.Exports;

namespace FinanceManager.Application.Transactions.Export;

public sealed class BondExportDtoMap : ClassMap<BondExportDto>
{
    public BondExportDtoMap()
    {
        var index = 0;
        Map(x => x.Id).Index(index++).Name("Id");
        Map(x => x.PostingDate).Index(index++).Name("PostingDate").TypeConverterOption.Format("yyyy-MM-ddTHH:mm:ssZ");
        Map(x => x.ValueChange).Index(index++).Name("ValueChange");
        Map(x => x.BondName).Index(index++).Name("Bond");
        Map(x => x.Description).Index(index).Name("Description");
    }
}