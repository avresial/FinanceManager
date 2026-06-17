using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Domain.Administration.Logging;

public class LogEntry
{
    public int Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public LogSeverity Level { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public int? EventId { get; set; }
    public string? EventName { get; set; }
}