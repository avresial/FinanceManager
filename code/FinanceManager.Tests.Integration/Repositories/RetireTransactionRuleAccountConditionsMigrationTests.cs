using FinanceManager.Domain.Identity.Dtos;
using FinanceManager.Domain.TransactionRules.Entities;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

/// <summary>Runs the real migration SQL on a disposable PostgreSQL against rule definitions serialised the way the application wrote them.</summary>
[Trait("Category", "Integration")]
public sealed class RetireTransactionRuleAccountConditionsMigrationTests
{
    private const string _previous = "AddOwnerTransactionHistoryIndexes";
    private const string _migration = "RetireTransactionRuleAccountConditions";
    private const int _userId = 1;
    private static readonly JsonSerializerOptions _web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Up_RemovesAccountConditions_DisablesOnlyAffectedRules_AndIsIdempotent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.SkipUnless(await DockerAvailable(cancellationToken), "Docker is required for the PostgreSQL migration test.");
        var containerName = $"fm-rules-migration-{Guid.NewGuid():N}";

        try
        {
            var connectionString = await StartPostgres(containerName, cancellationToken);
            await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString, b => b.MigrationsAssembly("FinanceManager.Api")).Options);
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(_previous, cancellationToken);

            context.Users.Add(new UserDto { Id = _userId, Login = "rules", Password = "x", CreationDate = DateTime.UtcNow });
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
            };
            // (UserId, Order) is unique.
            foreach (var (rule, order) in seeded.Values.Select((rule, index) => (rule, index + 1)))
                rule.Order = order;
            context.TransactionRules.AddRange(seeded.Values);
            await context.SaveChangesAsync(cancellationToken);
            var untouchedJson = seeded["untouched"].ConditionsJson;

            await migrator.MigrateAsync(_migration, cancellationToken);
            await AssertMigrated(context, seeded, untouchedJson, cancellationToken);

            // A second run (roll back the data-only step's history row, apply it again) must change nothing.
            var afterFirstRun = await Snapshot(context, cancellationToken);
            await migrator.MigrateAsync(_previous, cancellationToken);
            await migrator.MigrateAsync(_migration, cancellationToken);
            Assert.Equal(afterFirstRun, await Snapshot(context, cancellationToken));
        }
        finally
        {
            await DockerExitCode(CancellationToken.None, "rm", "--force", containerName);
        }
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
        UserId = _userId,
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