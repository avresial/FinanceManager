using CsvHelper.Configuration;

namespace FinanceManager.Components.DtoMapping;

public class ImportCurrencyModelMap : ClassMap<ImportCurrencyModel>
{
    public ImportCurrencyModelMap(string postingDateHeader, string valueChangeHeader)
    {
        Map(m => m.PostingDate).Name(postingDateHeader);
        Map(m => m.ValueChange).Name(valueChangeHeader);
    }
}