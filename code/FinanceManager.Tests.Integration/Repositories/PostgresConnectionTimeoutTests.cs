using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.ComponentModel;
using System.Diagnostics;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

[Trait("Category", "Integration")]
public sealed class PostgresConnectionTimeoutTests
{
    [Fact]
    public async Task PausedPostgres_ThrowsConnectionOpenTimeoutThroughEfCore()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.SkipUnless(await DockerAvailable(cancellationToken), "Docker is required for the PostgreSQL timeout test.");
        var containerName = $"fm-timeout-test-{Guid.NewGuid():N}";

        try
        {
            var connectionString = await StartPostgres(containerName, cancellationToken);
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
            await using var context = new AppDbContext(options);

            Assert.Equal(1, await context.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\"").SingleAsync(cancellationToken));
            NpgsqlConnection.ClearPool(Assert.IsType<NpgsqlConnection>(context.Database.GetDbConnection()));
            await Docker(cancellationToken, "pause", containerName);

            var repository = new CurrencyRepository(context);
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.GetCurrencies(cancellationToken).ToListAsync(cancellationToken).AsTask());

            var providerFailure = Assert.IsType<NpgsqlException>(failure.InnerException);
            Assert.IsType<TimeoutException>(providerFailure.InnerException);
            Assert.Contains("NpgsqlTimeout.CheckAndGetTimeLeft", failure.ToString());
            Assert.Contains("NpgsqlConnector.ConnectAsync", failure.ToString());
            Assert.Contains("PoolingDataSource.OpenNewConnector", failure.ToString());
            Assert.Contains("CurrencyRepository.EnsureDefaults", failure.ToString());
        }
        finally
        {
            await DockerExitCode(CancellationToken.None, "unpause", containerName);
            await DockerExitCode(CancellationToken.None, "rm", "--force", containerName);
        }
    }

    [Fact]
    public async Task RegisteredContext_RecoversWhenPostgresResumesAfterConnectionTimeout()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.SkipUnless(await DockerAvailable(cancellationToken), "Docker is required for the PostgreSQL recovery test.");
        var containerName = $"fm-timeout-test-{Guid.NewGuid():N}";

        try
        {
            var connectionString = await StartPostgres(containerName, cancellationToken);
            await using var provider = CreateProvider(connectionString);
            await using var scope = provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Assert.Equal(1, await context.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\"").SingleAsync(cancellationToken));
            NpgsqlConnection.ClearPool(Assert.IsType<NpgsqlConnection>(context.Database.GetDbConnection()));
            await Docker(cancellationToken, "pause", containerName);

            var restoreTask = Task.Run(async () =>
            {
                await Task.Delay(2500, cancellationToken);
                await Docker(cancellationToken, "unpause", containerName);
            }, cancellationToken);

            try
            {
                Assert.Equal(1, await context.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\"").SingleAsync(cancellationToken));

                await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                Assert.Equal(1, await context.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\"").SingleAsync(cancellationToken));
                await transaction.RollbackAsync(cancellationToken);
            }
            finally
            {
                await restoreTask;
            }
        }
        finally
        {
            await DockerExitCode(CancellationToken.None, "unpause", containerName);
            await DockerExitCode(CancellationToken.None, "rm", "--force", containerName);
        }
    }

    private static ServiceProvider CreateProvider(string connectionString)
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["DatabaseProvider"] = "PostgreSQL"
        });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDatabase(configuration);
        return services.BuildServiceProvider();
    }

    private static async Task<string> StartPostgres(string containerName, CancellationToken cancellationToken)
    {
        await Docker(cancellationToken, "run", "--detach", "--rm", "--name", containerName,
            "--env", "POSTGRES_HOST_AUTH_METHOD=trust", "--publish", "127.0.0.1::5432", "postgres:17");

        var portOutput = await Docker(cancellationToken, "port", containerName, "5432/tcp");
        var port = int.Parse(portOutput.Trim().Split(':').Last());
        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = port,
            Database = "postgres",
            Username = "postgres",
            Timeout = 2,
            CommandTimeout = 2,
            Pooling = true
        }.ConnectionString;

        for (var attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = new NpgsqlCommand("SELECT 1", connection);
                await command.ExecuteScalarAsync(cancellationToken);
                return connectionString;
            }
            catch (NpgsqlException) when (attempt < 39)
            {
                await Task.Delay(250, cancellationToken);
            }

        }

        throw new InvalidOperationException("PostgreSQL container did not become ready.");
    }

    private static async Task<bool> DockerAvailable(CancellationToken cancellationToken)
    {
        try
        {
            return await DockerExitCode(cancellationToken, "info", "--format", "{{.ServerVersion}}") == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static async Task<string> Docker(CancellationToken cancellationToken, params string[] arguments)
    {
        using var process = StartDocker(arguments);
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"docker {arguments[0]} failed: {error}");

        return output;
    }

    private static async Task<int> DockerExitCode(CancellationToken cancellationToken, params string[] arguments)
    {
        using var process = StartDocker(arguments);
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }

    private static Process StartDocker(string[] arguments)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start Docker.");
    }

}