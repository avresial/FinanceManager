using FinanceManager.Domain.Administration.Logging;

namespace FinanceManager.Components.Features.Administration.Components;

/// <summary>The fields of a log entry the recent warnings and errors card displays. <see cref="Id"/> gives identity and tie-break ordering.</summary>
public sealed record AdminLogEntryView(int Id, DateTime TimestampUtc, LogSeverity Level, string Category, string Message);