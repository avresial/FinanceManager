using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations;

/// <summary>
/// Moves Description to every transaction type: investment Notes becomes the shared Description (renamed, not
/// dropped, so existing notes survive) and bond entries gain their own Description column.
/// </summary>
public partial class AddDescriptionToAllTransactionTypes : Migration
{
    // The model snapshot is generated against PostgreSQL, so EF would otherwise reuse "character varying(512)" on
    // SQL Server. Spell out the 512-character column type for the active provider instead.
    private string DescriptionColumnType => ActiveProvider switch
    {
        "Npgsql.EntityFrameworkCore.PostgreSQL" => "character varying(512)",
        "Microsoft.EntityFrameworkCore.SqlServer" => "nvarchar(512)",
        _ => throw new NotSupportedException($"Unsupported migration provider: {ActiveProvider}")
    };

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The shared Description is non-null, so existing NULL notes become empty text first. This runs against the
        // old column name, before the rename: SQL Server compiles a whole script batch up front and would reject an
        // UPDATE that names the new column. The AlterColumn below therefore needs no default value of its own.
        migrationBuilder.Sql(ActiveProvider switch
        {
            "Npgsql.EntityFrameworkCore.PostgreSQL" => """UPDATE "InvestmentTransactions" SET "Notes" = '' WHERE "Notes" IS NULL;""",
            "Microsoft.EntityFrameworkCore.SqlServer" => "UPDATE [InvestmentTransactions] SET [Notes] = N'' WHERE [Notes] IS NULL;",
            _ => throw new NotSupportedException($"Unsupported migration provider: {ActiveProvider}")
        });

        migrationBuilder.RenameColumn(name: "Notes", table: "InvestmentTransactions", newName: "Description");

        migrationBuilder.AlterColumn<string>(
            name: "Description",
            table: "InvestmentTransactions",
            type: DescriptionColumnType,
            maxLength: 512,
            nullable: false,
            oldClrType: typeof(string),
            oldMaxLength: 512,
            oldNullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "BondEntries",
            type: DescriptionColumnType,
            maxLength: 512,
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Description", table: "BondEntries");

        migrationBuilder.AlterColumn<string>(
            name: "Description",
            table: "InvestmentTransactions",
            type: DescriptionColumnType,
            maxLength: 512,
            nullable: true,
            oldClrType: typeof(string),
            oldMaxLength: 512,
            oldNullable: false,
            oldDefaultValue: "");

        // Empty text carries no note, so it goes back to NULL like it was before this migration.
        migrationBuilder.Sql(ActiveProvider switch
        {
            "Npgsql.EntityFrameworkCore.PostgreSQL" => """UPDATE "InvestmentTransactions" SET "Description" = NULL WHERE "Description" = '';""",
            "Microsoft.EntityFrameworkCore.SqlServer" => "UPDATE [InvestmentTransactions] SET [Description] = NULL WHERE [Description] = N'';",
            _ => throw new NotSupportedException($"Unsupported migration provider: {ActiveProvider}")
        });

        migrationBuilder.RenameColumn(name: "Description", table: "InvestmentTransactions", newName: "Notes");
    }
}