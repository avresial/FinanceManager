using FinanceManager.Domain.Identity.Dtos;
using FinanceManager.Domain.TransactionRules.Entities;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

/// <summary>Runs the real migration SQL on a disposable PostgreSQL and SQL Server against rule definitions serialised the way the application wrote them.</summary>
[Trait("Category", "Integration")]
public sealed class RetireTransactionRuleAccountConditionsMigrationTests
{
    private const string _previous = "AddOwnerTransactionHistoryIndexes";
    private const string _migration = "RetireTransactionRuleAccountConditions";
    private static readonly JsonSerializerOptions _web = new(JsonSerializerDefaults.Web);

    private const string _sqlServerPassword = "Fm-Rules-Migration-1!";

    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public async Task Up_RemovesAccountConditions_DisablesOnlyAffectedRules_AndIsIdempotent(string provider)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.SkipUnless(await DockerAvailable(cancellationToken), $"Docker is required for the {provider} migration test.");
        var containerName = $"fm-rules-migration-{Guid.NewGuid():N}";

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>();
            if (provider == "PostgreSql")
                options.UseNpgsql(await StartPostgres(containerName, cancellationToken), b => b.MigrationsAssembly("FinanceManager.Api"));
            else
                options.UseSqlServer(await StartSqlServer(containerName, cancellationToken), b => b.MigrationsAssembly("FinanceManager.Api"));
            await using var context = new AppDbContext(options.Options);
            var migrator = context.GetService<IMigrator>();
            // The migration history starts with PostgreSQL-only DDL, so SQL Server cannot replay it: it gets the current model instead, and the
            // migration's own SQL is executed directly.
            if (provider == "PostgreSql") await migrator.MigrateAsync(_previous, cancellationToken);
            else await context.Database.EnsureCreatedAsync(cancellationToken);

            // The key is database generated on SQL Server.
            var user = new UserDto { Login = "rules", Password = "x", CreationDate = DateTime.UtcNow };
            context.Users.Add(user);
            await context.SaveChangesAsync(cancellationToken);
            var seeded = new Dictionary<string, TransactionRuleDefinition>
            {
                ["only-account"] = Rule([Account(1)]),
                ["many-accounts"] = Rule([Account(1), Account(2, 3)]),
                ["mixed"] = Rule([Contractor("ACME"), Account(4), Amount(10m)]),
                ["pascal-case"] = Rule(null, "[{\"Type\":\"Account\",\"AccountIds\":[7]},{\"Type\":\"Direction\",\"Direction\":1}]"),
                ["padded"] = Rule(null, "[{\"type\":\"  ACCOUNT \",\"accountIds\":[7]},{\"type\":\"Contractor\",\"pattern\":\"x\"}]"),
                ["untouched"] = Rule([Contractor("ACME"), Amount(10m)]),
                ["untouched-disabled"] = Rule([Contractor("ACME")], enabled: false),
                ["no-conditions"] = Rule([]),
                // ConditionsJson is plain text: rows that are not valid JSON must never fail the deploy and are left untouched.
                ["empty-text"] = Rule(null, ""),
                ["not-json"] = Rule(null, "not-json"),
                ["truncated"] = Rule(null, "[{\"type\":\"account\", broken"),
                ["mentions-account-not-json"] = Rule(null, "account account {"),
                ["object-not-array"] = Rule(null, "{\"type\":\"account\"}"),
                ["scalar"] = Rule(null, "\"account\""),
                ["null-element"] = Rule(null, "[null,{\"type\":\"Contractor\",\"pattern\":\"account\"}]"),
            };
            // PostgreSQL jsonb rejects the NUL escape; SQL Server keeps it as a valid JSON string.
            if (provider == "PostgreSql")
                seeded["nul-escape"] = Rule(null, "[{\"type\":\"account\\u0000\"}]");
            // (UserId, Order) is unique.
            foreach (var (rule, order) in seeded.Values.Select((rule, index) => (rule, index + 1)))
            {
                rule.UserId = user.Id;
                rule.Order = order;
            }
            context.TransactionRules.AddRange(seeded.Values);
            await context.SaveChangesAsync(cancellationToken);
            var untouchedJson = seeded["untouched"].ConditionsJson;
            var notJsonRows = seeded.Where(pair => pair.Key is "empty-text" or "not-json" or "truncated" or "mentions-account-not-json"
                    or "object-not-array" or "scalar" or "null-element" or "nul-escape")
                .ToDictionary(pair => pair.Value.Id, pair => pair.Value.ConditionsJson);

            await ApplyMigration(provider, context, migrator, cancellationToken);
            await AssertMigrated(context, seeded, untouchedJson, cancellationToken);
            foreach (var (id, json) in notJsonRows)
            {
                var rule = await context.TransactionRules.AsNoTracking().SingleAsync(r => r.Id == id, cancellationToken);
                Assert.Equal(json, rule.ConditionsJson);
                Assert.True(rule.IsEnabled);
                Assert.Null(rule.UpdatedAt);
            }

            // A second run (roll back the data-only step's history row, apply it again) must change nothing.
            var afterFirstRun = await Snapshot(context, cancellationToken);
            if (provider == "PostgreSql") await migrator.MigrateAsync(_previous, cancellationToken);
            await ApplyMigration(provider, context, migrator, cancellationToken);
            Assert.Equal(afterFirstRun, await Snapshot(context, cancellationToken));
        }
        finally
        {
            await DockerExitCode(CancellationToken.None, "rm", "--force", containerName);
        }
    }

    private static async Task ApplyMigration(string provider, AppDbContext context, IMigrator migrator, CancellationToken cancellationToken)
    {
        if (provider == "PostgreSql")
        {
            await migrator.MigrateAsync(_migration, cancellationToken);
            return;
        }

        var migration = new Api.Migrations.RetireTransactionRuleAccountConditions { ActiveProvider = context.Database.ProviderName! };
        foreach (var operation in migration.UpOperations.OfType<SqlOperation>())
            await context.Database.ExecuteSqlRawAsync(operation.Sql, cancellationToken);
    }

    private static async Task AssertMigrated(AppDbContext context, Dictionary<string, TransactionRuleDefinition> seeded, string untouchedJson, CancellationToken cancellationToken)
    {
        async Task<TransactionRuleDefinition> Load(string key) =>
            await context.TransactionRules.AsNoTracking().SingleAsync(rule => rule.Id == seeded[key].Id, cancellationToken);
        static string[] Types(TransactionRuleDefinition rule) =>
            [.. JsonDocument.Parse(rule.ConditionsJson).RootElement.EnumerateArray().Select(e => (e.TryGetProperty("type", out var t) || e.TryGetProperty("Type", out t) ? t.GetString() : null)!)];

        foreach (var key in new[] { "only-account", "many-accounts" })
        {
            var rule = await Load(key);
            Assert.Equal("[]", rule.ConditionsJson);
            Assert.False(rule.IsEnabled);
            Assert.NotNull(rule.UpdatedAt);
        }

        var mixed = await Load("mixed");
        Assert.Equal(["Contractor", "Amount"], Types(mixed));
        Assert.Equal("ACME", JsonDocument.Parse(mixed.ConditionsJson).RootElement[0].GetProperty("pattern").GetString());
        Assert.False(mixed.IsEnabled);
        Assert.Equal(["Direction"], Types(await Load("pascal-case")));
        Assert.Equal(["Contractor"], Types(await Load("padded")));
        Assert.False((await Load("pascal-case")).IsEnabled);
        Assert.False((await Load("padded")).IsEnabled);

        var untouched = await Load("untouched");
        Assert.True(untouched.IsEnabled);
        Assert.Equal(untouchedJson, untouched.ConditionsJson);
        Assert.Null(untouched.UpdatedAt);
        Assert.False((await Load("untouched-disabled")).IsEnabled);
        var noConditions = await Load("no-conditions");
        Assert.True(noConditions.IsEnabled);
        Assert.Equal("[]", noConditions.ConditionsJson);
        Assert.Null(noConditions.UpdatedAt);
    }

    private static async Task<List<(Guid Id, string Conditions, bool Enabled, DateTime? UpdatedAt)>> Snapshot(AppDbContext context, CancellationToken cancellationToken) =>
        [.. (await context.TransactionRules.AsNoTracking().OrderBy(rule => rule.Id).ToListAsync(cancellationToken))
            .Select(rule => (rule.Id, rule.ConditionsJson, rule.IsEnabled, rule.UpdatedAt))];

    private static TransactionRuleDefinition Rule(object[]? conditions, string? rawConditions = null, bool enabled = true) => new()
    {
        Name = Guid.NewGuid().ToString("N"),
        IsEnabled = enabled,
        ConditionsJson = rawConditions ?? JsonSerializer.Serialize(conditions, _web),
        ActionsJson = "[{\"type\":\"SetLabels\",\"labels\":[\"Bills\"],\"replaceExisting\":false}]"
    };

    // The condition DTO as the application serialised it (web casing, every property present).
    private static object Account(params int[] accountIds) => new
    {
        type = "Account",
        pattern = (string?)null,
        matchOperator = 1,
        ignoreCase = true,
        accountIds,
        direction = 0,
        threshold = (decimal?)null,
        comparison = 4,
        minAmount = (decimal?)null,
        maxAmount = (decimal?)null
    };

    private static object Contractor(string pattern) => new
    {
        type = "Contractor",
        pattern,
        matchOperator = 1,
        ignoreCase = true,
        accountIds = Array.Empty<int>(),
        direction = 0,
        threshold = (decimal?)null,
        comparison = 4,
        minAmount = (decimal?)null,
        maxAmount = (decimal?)null
    };

    private static object Amount(decimal threshold) => new
    {
        type = "Amount",
        pattern = (string?)null,
        matchOperator = 1,
        ignoreCase = true,
        accountIds = Array.Empty<int>(),
        direction = 0,
        threshold = (decimal?)threshold,
        comparison = 4,
        minAmount = (decimal?)null,
        maxAmount = (decimal?)null
    };

    private static async Task<string> StartPostgres(string containerName, CancellationToken cancellationToken)
    {
        await Docker(cancellationToken, "run", "--detach", "--rm", "--name", containerName,
            "--env", "POSTGRES_HOST_AUTH_METHOD=trust", "--publish", "127.0.0.1::5432", "postgres:17");

        var portOutput = await Docker(cancellationToken, "port", containerName, "5432/tcp");
        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = int.Parse(portOutput.Trim().Split(':').Last()),
            Database = "postgres",
            Username = "postgres",
            Timeout = 5,
            CommandTimeout = 60
        }.ConnectionString;

        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                return connectionString;
            }
            catch (NpgsqlException) when (attempt < 59)
            {
                await Task.Delay(250, cancellationToken);
            }
        }

        throw new InvalidOperationException("PostgreSQL container did not become ready.");
    }

    private static async Task<string> StartSqlServer(string containerName, CancellationToken cancellationToken)
    {
        await Docker(cancellationToken, "run", "--detach", "--rm", "--name", containerName,
            "--env", "ACCEPT_EULA=Y", "--env", $"MSSQL_SA_PASSWORD={_sqlServerPassword}", "--publish", "127.0.0.1::1433",
            "mcr.microsoft.com/mssql/server:2022-latest");

        var portOutput = await Docker(cancellationToken, "port", containerName, "1433/tcp");
        var connectionString = new SqlConnectionStringBuilder
        {
            DataSource = $"127.0.0.1,{portOutput.Trim().Split(':').Last()}",
            InitialCatalog = "master",
            UserID = "sa",
            Password = _sqlServerPassword,
            TrustServerCertificate = true,
            ConnectTimeout = 5,
            CommandTimeout = 120
        }.ConnectionString;

        // SQL Server needs noticeably longer than PostgreSQL to accept logins, especially under emulation.
        for (var attempt = 0; attempt < 240; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                return new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "FmRulesMigration" }.ConnectionString;
            }
            catch (SqlException) when (attempt < 239)
            {
                await Task.Delay(500, cancellationToken);
            }
        }

        throw new InvalidOperationException("SQL Server container did not become ready.");
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
        var result = await Process.RunAndCaptureTextAsync("docker", arguments, cancellationToken);
        if (result.ExitStatus.Canceled) throw new OperationCanceledException(cancellationToken);
        if (result.ExitStatus.ExitCode != 0)
            throw new InvalidOperationException($"docker {arguments[0]} failed: {result.StandardError}");

        return result.StandardOutput;
    }

    private static async Task<int> DockerExitCode(CancellationToken cancellationToken, params string[] arguments)
    {
        var status = await Process.RunAsync("docker", arguments, silent: true, cancellationToken);
        if (status.Canceled) throw new OperationCanceledException(cancellationToken);
        return status.ExitCode;
    }
}