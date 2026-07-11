using CsvHelper.Configuration;
using FinanceManager.Domain.FinancialAccounts.Bond.Exports;
using FinanceManager.Domain.FinancialAccounts.Shared.Exports;

namespace FinanceManager.Application.FinancialAccounts.Bond.Export;

public sealed class BondAccountExportDtoMap : ClassMap<BondAccountExportDto>
{
    public BondAccountExportDtoMap()
    {
        Map(x => x.Id).Index(0).Name("Id");
        Map(x => x.PostingDate).Index(1).Name("PostingDate").TypeConverterOption.Format("yyyy-MM-ddTHH:mm:ssZ");
        Map(x => x.Value).Index(2).Name("Value");
        Map(x => x.ValueChange).Index(3).Name("ValueChange");
        Map(x => x.BondName).Index(4).Name("Bond");
    }
}