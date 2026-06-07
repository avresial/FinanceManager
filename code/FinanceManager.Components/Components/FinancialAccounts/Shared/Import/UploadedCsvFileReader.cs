using Microsoft.AspNetCore.Components.Forms;

namespace FinanceManager.Components.Components.FinancialAccounts.Shared.Import;

public static class UploadedCsvFileReader
{
    private const long MaxFileSize = 20 * 1024 * 1024;

    public static async Task<UploadedCsvFileReadResult> ReadAsync(IBrowserFile? file)
    {
        if (file is null)
            return UploadedCsvFileReadResult.Failed("No file selected.");

        if (!Path.GetExtension(file.Name).Equals(".csv", StringComparison.InvariantCultureIgnoreCase))
            return UploadedCsvFileReadResult.Failed($"{file.Name} is not a csv file. Select csv file to continue.");

        using var stream = file.OpenReadStream(maxAllowedSize: MaxFileSize);
        using StreamReader reader = new(stream);

        var content = await reader.ReadToEndAsync();
        return string.IsNullOrWhiteSpace(content)
            ? UploadedCsvFileReadResult.Failed("File is empty.")
            : UploadedCsvFileReadResult.Succeeded(file.Name, file.Size, content);
    }
}

public sealed record UploadedCsvFileReadResult(
    bool Success,
    string? Error,
    string? FileName,
    long FileSize,
    string? Content)
{
    public static UploadedCsvFileReadResult Succeeded(string fileName, long fileSize, string content) =>
        new(true, null, fileName, fileSize, content);

    public static UploadedCsvFileReadResult Failed(string error) =>
        new(false, error, null, 0, null);
}
