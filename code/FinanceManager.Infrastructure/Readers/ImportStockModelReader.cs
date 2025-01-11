using CsvHelper;
using CsvHelper.Configuration;
using FinanceManager.Infrastructure.DtoMapping;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.AspNetCore.Components.Forms;

namespace FinanceManager.Infrastructure.Readers
{
    public static class ImportStockModelReader
    {
        public static async Task<List<ImportStockModel>> Read(CsvConfiguration config, IBrowserFile file, string postingDateHeader, string valueChangeHeader)
        {
            var result = new List<ImportStockModel>();
            using (var reader = new StreamReader(file.OpenReadStream()))
            using (var csv = new CsvReader(reader, config))
            {
                csv.Context.RegisterClassMap(new ImportStockModelMap(postingDateHeader, valueChangeHeader));
                result = await csv.GetRecordsAsync<ImportStockModel>().ToListAsync();
            }
            return result;
        }
    }
}
