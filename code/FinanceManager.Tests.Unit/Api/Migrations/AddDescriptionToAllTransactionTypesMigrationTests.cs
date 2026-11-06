using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Text.RegularExpressions;

namespace FinanceManager.Tests.Unit.Api.Migrations;

public class AddDescriptionToAllTransactionTypesMigrationTests
{
    private const string _previousMigration = "StoreInvestmentPostingDate";
    private const string _migration = "AddDescriptionToAllTransactionTypes";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UpScript_RenamesNotesBackfillsNullsAndAddsBondDescription(bool sqlServer)
    {
        var up = GenerateScript(sqlServer, _previousMigration, _migration);

        // Renaming (not dropping) keeps the existing notes.
        Assert.DoesNotMatch($@"DROP COLUMN {Name("Notes")}", up);
        Assert.Matches(sqlServer
            ? @"sp_rename N'\[InvestmentTransactions\]\.\[Notes\]', N'Description', 'COLUMN'"
            : $@"ALTER TABLE {Name("InvestmentTransactions")} RENAME COLUMN {Name("Notes")} TO {Name("Description")}", up);

        // NULL notes become empty text, on the old column name and before the column turns non-null.
        var backfill = Regex.Match(up, $@"UPDATE {Name("InvestmentTransactions")} SET {Name("Notes")} = N?'' WHERE {Name("Notes")} IS NULL");
        Assert.True(backfill.Success, up);
        var rename = Regex.Match(up, "RENAME COLUMN|sp_rename");
        var tighten = Regex.Match(up, $@"ALTER COLUMN {Name("Description")} (TYPE )?\S+ NOT NULL|ALTER COLUMN {Name("Description")} SET NOT NULL");
        Assert.True(tighten.Success, up);
        Assert.True(backfill.Index < rename.Index && rename.Index < tighten.Index);

        // The snapshot is PostgreSQL-typed; SQL Server must not inherit its column type.
        if (sqlServer) Assert.DoesNotContain("character varying", up);
        Assert.Matches($@"ALTER TABLE {Name("BondEntries")} ADD {Name("Description")} (character varying\(512\)|nvarchar\(512\)) NOT NULL", up);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DownScript_RestoresNullNotesFromEmptyAndRenamesBack(bool sqlServer)
    {
        var down = GenerateScript(sqlServer, _migration, _previousMigration);

        var restore = Regex.Match(down, $@"UPDATE {Name("InvestmentTransactions")} SET {Name("Description")} = NULL WHERE {Name("Description")} = N?''");
        Assert.True(restore.Success, down);
        var nullable = Regex.Match(down, $@"ALTER COLUMN {Name("Description")} (TYPE \S+|DROP NOT NULL|nvarchar\(512\) NULL)");
        Assert.True(nullable.Success, down);
        Assert.True(nullable.Index < restore.Index);

        Assert.Matches($@"ALTER TABLE {Name("BondEntries")} DROP COLUMN {Name("Description")}", down);
        Assert.Matches(sqlServer
            ? @"sp_rename N'\[InvestmentTransactions\]\.\[Description\]', N'Notes', 'COLUMN'"
            : $@"ALTER TABLE {Name("InvestmentTransactions")} RENAME COLUMN {Name("Description")} TO {Name("Notes")}", down);
    }

    private static string GenerateScript(bool sqlServer, string from, string to)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        if (sqlServer)
            options.UseSqlServer("Server=localhost;Database=script-only;Trusted_Connection=True", b => b.MigrationsAssembly("FinanceManager.Api"));
        else
            options.UseNpgsql("Host=localhost;Database=script-only", b => b.MigrationsAssembly("FinanceManager.Api"));
        using var context = new AppDbContext(options.Options);
        return context.GetService<IMigrator>().GenerateScript(from, to);
    }

    // Quoting differs by provider: "Name" on PostgreSQL and [Name] on SQL Server.
    private static string Name(string identifier) => $@"[""\[]{Regex.Escape(identifier)}[""\]]";
}