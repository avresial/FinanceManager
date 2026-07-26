using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Persistence;

/// <summary>
/// Identifies the EF Core provider behind an <see cref="DbContext"/> so the repositories can pick a SQL
/// dialect for the hand-written set-based statements.
/// </summary>
/// <remarks>
/// The running-balance recalculations are expressed as a single windowed UPDATE, which has no portable
/// spelling — SQL Server, PostgreSQL and SQLite each want a different one. Deciding by provider name in
/// one place keeps that dialect switch consistent between the currency and bond calculators, and gives
/// them a shared answer to "is this a provider we have SQL for?" so an unrecognised one can take the
/// portable managed path instead of being handed statements it cannot parse.
/// </remarks>
internal static class DatabaseProviders
{
    private const string _sqlitePrefix = "Microsoft.EntityFrameworkCore.Sqlite";
    private const string _npgsqlPrefix = "Npgsql";
    private const string _sqlServerPrefix = "Microsoft.EntityFrameworkCore.SqlServer";

    /// <summary>True when the provider is SQLite (used by benchmarks and local tooling).</summary>
    public static bool IsSqlite(DbContext context) => HasPrefix(context, _sqlitePrefix);

    /// <summary>True when the provider is PostgreSQL.</summary>
    public static bool IsNpgsql(DbContext context) => HasPrefix(context, _npgsqlPrefix);

    /// <summary>True when the provider is SQL Server.</summary>
    public static bool IsSqlServer(DbContext context) => HasPrefix(context, _sqlServerPrefix);

    /// <summary>
    /// True when a hand-written set-based UPDATE exists for this provider. False means the caller must
    /// use its managed row-by-row fallback, which is correct on every provider but slower.
    /// </summary>
    public static bool HasSetBasedRecalculation(DbContext context) =>
        IsSqlServer(context) || IsNpgsql(context) || IsSqlite(context);

    private static bool HasPrefix(DbContext context, string prefix) =>
        context.Database.ProviderName?.StartsWith(prefix, StringComparison.Ordinal) == true;
}