using CsvHelper;
using CsvHelper.Configuration;
using FinanceManager.Infrastructure.DtoMapping;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.AspNetCore.Components.Forms;

namespace FinanceManager.Infrastructure.Readers
{
    public static class ImportBankModelReader
    {
        public static async Task<List<ImportBankModel>> Read(CsvConfiguration config, IBrowserFile file, string postingDateHeader, string valueChangeHeader)
        {
            List<ImportBankModel> result = new List<ImportBankModel>();
            using (var reader = new StreamReader(file.OpenReadStream()))
            using (var csv = new CsvReader(reader, config))
            {
                csv.Context.RegisterClassMap(new ImportBankModelMap(postingDateHeader, valueChangeHeader));
                result = await csv.GetRecordsAsync<ImportBankModel>().ToListAsync();
            }
            return result;
        }
    }
}
