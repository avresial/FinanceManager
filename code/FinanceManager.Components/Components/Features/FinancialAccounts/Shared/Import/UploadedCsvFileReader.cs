using Microsoft.AspNetCore.Components.Forms;
using System.Text;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.Shared.Import;

public static class UploadedCsvFileReader
{
    private const long _maxFileSize = 20 * 1024 * 1024;
    private static readonly Encoding _strictUtf8 = new UTF8Encoding(false, true);
    private static readonly Encoding _windows1250 = CreateWindows1250Encoding();

    public static async Task<UploadedCsvFileReadResult> ReadAsync(IBrowserFile? file)
    {
        if (file is null)
            return UploadedCsvFileReadResult.Failed("No file selected.");

        if (!Path.GetExtension(file.Name).Equals(".csv", StringComparison.InvariantCultureIgnoreCase))
            return UploadedCsvFileReadResult.Failed($"{file.Name} is not a csv file. Select csv file to continue.");

        using var stream = file.OpenReadStream(maxAllowedSize: _maxFileSize);
        using MemoryStream buffer = new();
        await stream.CopyToAsync(buffer);

        var content = Decode(buffer.ToArray());
        return string.IsNullOrWhiteSpace(content)
            ? UploadedCsvFileReadResult.Failed("File is empty.")
            : UploadedCsvFileReadResult.Succeeded(file.Name, file.Size, content);
    }

    private static string Decode(byte[] bytes)
    {
        ReadOnlySpan<byte> content = bytes;
        if (content.StartsWith(_strictUtf8.Preamble))
            content = content[_strictUtf8.Preamble.Length..];

        try
        {
            return _strictUtf8.GetString(content);
        }
        catch (DecoderFallbackException)
        {
            return _windows1250.GetString(bytes);
        }
    }

    private static Encoding CreateWindows1250Encoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1250);
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