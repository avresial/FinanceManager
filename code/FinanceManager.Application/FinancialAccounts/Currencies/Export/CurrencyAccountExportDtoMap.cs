using CsvHelper.Configuration;
using FinanceManager.Domain.FinancialAccounts.Currencies.Exports;
using FinanceManager.Domain.FinancialAccounts.Shared.Exports;

namespace FinanceManager.Application.FinancialAccounts.Currencies.Export;

public sealed class CurrencyAccountExportDtoMap : ClassMap<CurrencyAccountExportDto>
{
    public CurrencyAccountExportDtoMap()
    {
        var index = 0;
        Map(x => x.Id).Index(index++).Name("Id");
        Map(x => x.PostingDate).Index(index++).Name("PostingDate").TypeConverterOption.Format("yyyy-MM-ddTHH:mm:ssZ");
        Map(x => x.ValueChange).Index(index++).Name("ValueChange");
        Map(x => x.ContractorDetails).Index(index++).Name("ContractorDetails");
        Map(x => x.Description).Index(index).Name("Description");
    }
}