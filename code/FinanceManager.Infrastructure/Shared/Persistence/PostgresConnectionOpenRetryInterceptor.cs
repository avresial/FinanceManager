using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data.Common;

namespace FinanceManager.Infrastructure.Shared.Persistence;

internal sealed class PostgresConnectionOpenRetryInterceptor(
    ILogger<PostgresConnectionOpenRetryInterceptor> logger) : DbConnectionInterceptor
{
    public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (result.IsSuppressed || connection is not NpgsqlConnection)
            return result;

        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch (NpgsqlException exception) when (exception.InnerException is TimeoutException && !cancellationToken.IsCancellationRequested)
        {
            // Opening a connection has not run application SQL, so this retry cannot replay a write.
            logger.LogWarning(exception, "PostgreSQL connection open timed out; retrying once.");
            await Task.Delay(250, cancellationToken);
            await connection.OpenAsync(cancellationToken);
        }

        return InterceptionResult.Suppress();
    }
}