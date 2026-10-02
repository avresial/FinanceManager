using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLifetimeGiftCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var sqlServer = ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer";
            migrationBuilder.CreateTable(
                name: "GiftCodes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodeHash = table.Column<string>(type: sqlServer ? "nvarchar(64)" : "character varying(64)", maxLength: 64, nullable: false),
                    CodeSuffix = table.Column<string>(type: sqlServer ? "nvarchar(4)" : "character varying(4)", maxLength: 4, nullable: false),
                    PricingLevel = table.Column<int>(type: sqlServer ? "int" : "integer", nullable: false),
                    Note = table.Column<string>(type: sqlServer ? "nvarchar(200)" : "character varying(200)", maxLength: 200, nullable: true),
                    State = table.Column<int>(type: sqlServer ? "int" : "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: sqlServer ? "datetime2" : "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: sqlServer ? "int" : "integer", nullable: false),
                    RedeemedAtUtc = table.Column<DateTime>(type: sqlServer ? "datetime2" : "timestamp with time zone", nullable: true),
                    RedeemedByUserId = table.Column<int>(type: sqlServer ? "int" : "integer", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: sqlServer ? "datetime2" : "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<int>(type: sqlServer ? "int" : "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftCodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GiftCodes_CodeHash",
                table: "GiftCodes",
                column: "CodeHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GiftCodes");
        }
    }
}