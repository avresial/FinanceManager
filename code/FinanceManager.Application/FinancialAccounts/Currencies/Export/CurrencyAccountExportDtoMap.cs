using CsvHelper.Configuration;
using FinanceManager.Domain.FinancialAccounts.Currencies.Exports;
using FinanceManager.Domain.FinancialAccounts.Shared.Exports;

namespace FinanceManager.Application.FinancialAccounts.Currencies.Export;

public sealed class CurrencyAccountExportDtoMap : ClassMap<CurrencyAccountExportDto>
{
    public CurrencyAccountExportDtoMap()
    {
        Map(x => x.Id).Index(0).Name("Id");
        Map(x => x.PostingDate).Index(1).Name("PostingDate").TypeConverterOption.Format("yyyy-MM-ddTHH:mm:ssZ");
        Map(x => x.Value).Index(2).Name("Value");
        Map(x => x.ValueChange).Index(3).Name("ValueChange");
        Map(x => x.ContractorDetails).Index(4).Name("ContractorDetails");
        Map(x => x.Description).Index(5).Name("Description");
    }
}