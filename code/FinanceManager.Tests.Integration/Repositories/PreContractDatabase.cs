using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace FinanceManager.Tests.Integration.Repositories;

/// <summary>
/// A disposable PostgreSQL or SQL Server database in the schema state before <c>DropFinancialAccounts</c>, for the live
/// migration tests. The current model no longer describes that schema, so rows are written with plain SQL.
/// PostgreSQL replays the real migration chain. That chain is PostgreSQL-typed and cannot run on SQL Server, so SQL Server
/// starts from the current model and re-adds the account objects with the names the migrations gave them.
/// </summary>
internal sealed class PreContractDatabase : IAsyncDisposable
{
    private PreContractDatabase(AppDbContext context, bool sqlServer) => (Context, SqlServer) = (context, sqlServer);

    public AppDbContext Context { get; }
    public bool SqlServer { get; }

    public static string ConnectionVariable(bool sqlServer) => sqlServer ? "FM_VALUE_SQLSERVER_CONNECTION" : "FM_VALUE_POSTGRESQL_CONNECTION";

    public static async Task<PreContractDatabase> Create(bool sqlServer, string connection, CancellationToken ct)
    {
        var database = $"fm_contract_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>();
        if (sqlServer)
            options.UseSqlServer(new SqlConnectionStringBuilder(connection) { InitialCatalog = database }.ConnectionString, b => b.MigrationsAssembly("FinanceManager.Api"));
        else
            options.UseNpgsql(new NpgsqlConnectionStringBuilder(connection) { Database = database }.ConnectionString, b => b.MigrationsAssembly("FinanceManager.Api"));
        var result = new PreContractDatabase(new AppDbContext(options.Options), sqlServer);
        if (sqlServer)
        {
            await result.Context.Database.EnsureCreatedAsync(ct);
            foreach (var statement in _sqlServerAccountObjects)
                await result.Execute(statement, ct);
        }
        else
        {
            await result.Context.GetService<IMigrator>().MigrateAsync("EnforceTransactionOwnerKeys", ct);
        }

        await result.Context.Database.OpenConnectionAsync(ct);
        return result;
    }

    // The account objects as the migrations created them; InvestmentTransactions.Value got a default from StoreInvestmentRunningUnits.
    private static readonly string[] _sqlServerAccountObjects =
    [
        """CREATE TABLE "Accounts" ("AccountId" int IDENTITY NOT NULL CONSTRAINT "PK_Accounts" PRIMARY KEY, "UserId" int NOT NULL, "Name" nvarchar(max) NOT NULL, "AccountType" int NOT NULL, "AccountLabel" int NOT NULL, "CurrencyId" int NOT NULL)""",
        """CREATE INDEX "IX_Accounts_AccountType_UserId" ON "Accounts" ("AccountType", "UserId")""",
        """ALTER TABLE "CurrencyEntries" ADD "AccountId" int NULL, "Value" decimal(18,2) NOT NULL""",
        """ALTER TABLE "BondEntries" ADD "AccountId" int NULL, "Value" decimal(18,2) NOT NULL""",
        """ALTER TABLE "InvestmentTransactions" ADD "AccountId" int NULL, "Value" decimal(18,8) NOT NULL DEFAULT 0.0""",
        """ALTER TABLE "DividendPayments" ADD "AccountId" int NULL CONSTRAINT "FK_DividendPayments_Accounts_AccountId" REFERENCES "Accounts" ("AccountId")""",
        """ALTER TABLE "FinancialAlerts" ADD "AccountId" int NULL""",
        """ALTER TABLE "FinancialInsights" ADD "AccountId" int NULL""",
        """CREATE INDEX "IX_CurrencyEntries_AccountId_PostingDate_EntryId" ON "CurrencyEntries" ("AccountId", "PostingDate", "EntryId")""",
        """CREATE INDEX "IX_BondEntries_AccountId_PostingDate_EntryId" ON "BondEntries" ("AccountId", "PostingDate", "EntryId")""",
        """CREATE INDEX "IX_BondEntries_AccountId_BondDetailsId_PostingDate_EntryId" ON "BondEntries" ("AccountId", "BondDetailsId", "PostingDate", "EntryId")""",
        """CREATE INDEX "IX_InvestmentTransactions_AccountId" ON "InvestmentTransactions" ("AccountId")""",
        """CREATE INDEX "IX_InvestmentTransactions_AccountId_PostingDate_Id" ON "InvestmentTransactions" ("AccountId", "PostingDate", "Id")""",
        """CREATE INDEX "IX_DividendPayments_AccountId" ON "DividendPayments" ("AccountId")""",
        """CREATE INDEX "IX_DividendPayments_UserId_AccountId_PaymentDate_Id" ON "DividendPayments" ("UserId", "AccountId", "PaymentDate", "Id")""",
    ];

    /// <summary>Applies a migration's operations in one transaction, as <c>Migrate</c> would, without the history row.</summary>
    public async Task Apply(Migration migration, bool up, CancellationToken ct)
    {
        migration.ActiveProvider = Context.Database.ProviderName!;
        await using var transaction = await Context.Database.BeginTransactionAsync(ct);
        foreach (var command in Context.GetService<IMigrationsSqlGenerator>().Generate(up ? migration.UpOperations : migration.DownOperations))
            await Context.Database.ExecuteSqlRawAsync(command.CommandText, ct);
        await transaction.CommitAsync(ct);
    }

    public Task<int> Execute(string sql, CancellationToken ct) => Context.Database.ExecuteSqlRawAsync(sql, ct);

    public async Task<long> Scalar(string sql, CancellationToken ct)
    {
        await using var command = Context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
    }

    /// <summary>Inserts one row and returns its generated <paramref name="identity"/> value, or 0 without one.</summary>
    public async Task<long> Insert(string table, string? identity, CancellationToken ct, params (string Column, object? Value)[] values)
    {
        await using var command = Context.Database.GetDbConnection().CreateCommand();
        var columns = string.Join(", ", values.Select(value => $"\"{value.Column}\""));
        var parameters = string.Join(", ", values.Select((_, index) => $"@p{index}"));
        var output = identity is null || !SqlServer ? "" : $" OUTPUT INSERTED.\"{identity}\"";
        var returning = identity is null || SqlServer ? "" : $" RETURNING \"{identity}\"";
        command.CommandText = $"INSERT INTO \"{table}\" ({columns}){output} VALUES ({parameters}){returning}";
        for (var index = 0; index < values.Length; index++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $"@p{index}";
            parameter.Value = values[index].Value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        if (identity is null)
        {
            await command.ExecuteNonQueryAsync(ct);
            return 0;
        }

        return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
    }

    public async ValueTask DisposeAsync()
    {
        await Context.Database.CloseConnectionAsync();
        await Context.Database.EnsureDeletedAsync(CancellationToken.None);
        await Context.DisposeAsync();
    }
}