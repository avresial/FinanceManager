using CsvHelper.Configuration;
using FinanceManager.Domain.FinancialAccounts.Bond.Exports;
using FinanceManager.Domain.FinancialAccounts.Shared.Exports;

namespace FinanceManager.Application.FinancialAccounts.Bond.Export;

public sealed class BondAccountExportDtoMap : ClassMap<BondAccountExportDto>
{
    public BondAccountExportDtoMap()
    {
        var index = 0;
        Map(x => x.Id).Index(index++).Name("Id");
        Map(x => x.PostingDate).Index(index++).Name("PostingDate").TypeConverterOption.Format("yyyy-MM-ddTHH:mm:ssZ");
        Map(x => x.ValueChange).Index(index++).Name("ValueChange");
        Map(x => x.BondName).Index(index++).Name("Bond");
        Map(x => x.Description).Index(index).Name("Description");
    }
}