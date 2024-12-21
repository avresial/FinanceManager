using CsvHelper;
using CsvHelper.Configuration;
using FinanceManager.Infrastructure.DtoMapping;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.AspNetCore.Components.Forms;

namespace FinanceManager.Infrastructure.Readers
{
    public static class ImportStockExtendedModelReader
    {
        public static async Task<List<ImportStockExtendedModel>> Read(CsvConfiguration config, IBrowserFile file, string postingDateHeader, string valueChangeHeader,
            string tickerChangeHeader, string investmentTypeChangeHeader)
        {
            List<ImportStockExtendedModel> result = new List<ImportStockExtendedModel>();
            using (var reader = new StreamReader(file.OpenReadStream()))
            using (var csv = new CsvReader(reader, config))
            {
                csv.Context.RegisterClassMap(new ImportStockExtendedModelMap(postingDateHeader, valueChangeHeader, tickerChangeHeader, investmentTypeChangeHeader));
                result = await csv.GetRecordsAsync<ImportStockExtendedModel>().ToListAsync();
            }
            return result;
        }
    }
}
