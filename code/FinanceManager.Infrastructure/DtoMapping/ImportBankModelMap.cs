using CsvHelper.Configuration;
using FinanceManager.Infrastructure.Dtos;

namespace FinanceManager.Infrastructure.DtoMapping;

public class ImportBankModelMap : ClassMap<ImportBankModel>
{
    public ImportBankModelMap(string postingDateHeader, string valueChangeHeader)
    {
        Map(m => m.PostingDate).Name(postingDateHeader);
        Map(m => m.ValueChange).Name(valueChangeHeader);
    }
}
