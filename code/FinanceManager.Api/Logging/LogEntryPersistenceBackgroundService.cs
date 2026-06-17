using FinanceManager.Api.Hubs;
using FinanceManager.Domain.Administration.Logging;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Repositories;
using Microsoft.AspNetCore.SignalR;

namespace FinanceManager.Api.Logging;

public sealed class LogEntryPersistenceBackgroundService(
    ILogEntryQueue queue,
    IServiceScopeFactory scopeFactory,
    IHubContext<AdminLogsHub> hubContext,
    ILogger<LogEntryPersistenceBackgroundService> logger) : BackgroundService
{
    private const int _maxBatchSize = 50;
    private static readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var _ = DatabaseLogger.BeginSuppression();
        logger.LogInformation("Log persistence background service started.");

        var buffer = new List<LogEntry>(_maxBatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await queue.Reader.WaitToReadAsync(stoppingToken))
                    break;

                using var batchTimeout = new CancellationTokenSource(_flushInterval);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(batchTimeout.Token, stoppingToken);

                buffer.Clear();
                try
                {
                    while (buffer.Count < _maxBatchSize && queue.Reader.TryRead(out var entry))
                        buffer.Add(entry);

                    while (buffer.Count < _maxBatchSize && await queue.Reader.WaitToReadAsync(linked.Token))
                    {
                        while (buffer.Count < _maxBatchSize && queue.Reader.TryRead(out var entry))
                            buffer.Add(entry);
                    }
                }
                catch (OperationCanceledException) when (batchTimeout.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
                {
                    // flush whatever we collected
                }

                if (buffer.Count == 0) continue;

                await Flush(buffer, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                using var __ = DatabaseLogger.BeginSuppression();
                logger.LogError(ex, "Failed to persist log batch of {Count} entries.", buffer.Count);
                // brief backoff to avoid tight failure loop
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        logger.LogInformation("Log persistence background service stopped.");
    }

    private async Task Flush(List<LogEntry> entries, CancellationToken cancellationToken)
    {
        using var _ = DatabaseLogger.BeginSuppression();

        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ILogEntryRepository>();
        await repository.AddRange(entries, cancellationToken);

        var payload = entries
            .Select(e => new LogEntryDto(
                e.Id,
                e.TimestampUtc,
                e.Level,
                e.Category,
                e.Message,
                e.Exception,
                e.EventId,
                e.EventName))
            .ToArray();

        try
        {
            await hubContext.Clients.Group(AdminLogsHub.GroupName).SendAsync("LogsAppended", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to broadcast log batch over SignalR.");
        }
    }
}