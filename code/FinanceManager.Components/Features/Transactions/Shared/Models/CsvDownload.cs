namespace FinanceManager.Components.Features.Transactions.Shared.Models;

/// <summary>A CSV file returned by the server: its bytes and the file name the server chose.</summary>
public record CsvDownload(byte[] Content, string FileName);