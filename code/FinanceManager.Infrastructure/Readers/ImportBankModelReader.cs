using CsvHelper;
using CsvHelper.Configuration;
using FinanceManager.Infrastructure.DtoMapping;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.AspNetCore.Components.Forms;

namespace FinanceManager.Infrastructure.Readers;

public static class ImportBankModelReader
{
    public static async Task<List<ImportBankModel>> Read(CsvConfiguration config, IBrowserFile file, string postingDateHeader, string valueChangeHeader)
    {

        using var reader = new StreamReader(file.OpenReadStream());
        using var csv = new CsvReader(reader, config);

        csv.Context.RegisterClassMap(new ImportBankModelMap(postingDateHeader, valueChangeHeader));
        return await csv.GetRecordsAsync<ImportBankModel>().ToListAsync();
    }

    public static async Task<List<string>> Read(IBrowserFile file, string separator)
    {
        var stream = new MemoryStream();
        await file.OpenReadStream().CopyToAsync(stream);
        var outputFileString = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        outputFileString = outputFileString.Replace("\"", string.Empty);
        return outputFileString.Split(Environment.NewLine).ToList();
    }
}
