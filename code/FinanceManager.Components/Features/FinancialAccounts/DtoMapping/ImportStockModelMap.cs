using CsvHelper.Configuration;

namespace FinanceManager.Components.Features.FinancialAccounts.DtoMapping;

public sealed class ImportStockModelMap : ClassMap<ImportStockModel>
{
    public ImportStockModelMap(string postingDateHeader, string valueChangeHeader)
    {
        Map(m => m.PostingDate).Name(postingDateHeader);
        Map(m => m.ValueChange).Name(valueChangeHeader);
    }
}