using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDividendPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var sqlServer = ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer";
            migrationBuilder.CreateTable(
                name: "DividendPayments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: sqlServer ? "int" : "integer", nullable: false),
                    AccountId = table.Column<int>(type: sqlServer ? "int" : "integer", nullable: false),
                    AssetListingId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: sqlServer ? "decimal(18,8)" : "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Currency = table.Column<string>(type: sqlServer ? "nvarchar(3)" : "character varying(3)", maxLength: 3, nullable: false),
                    Notes = table.Column<string>(type: sqlServer ? "nvarchar(512)" : "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: sqlServer ? "datetimeoffset" : "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: sqlServer ? "datetimeoffset" : "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DividendPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DividendPayments_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DividendPayments_AssetListings_AssetListingId",
                        column: x => x.AssetListingId,
                        principalTable: "AssetListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DividendPayments_AccountId",
                table: "DividendPayments",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_DividendPayments_AssetListingId",
                table: "DividendPayments",
                column: "AssetListingId");

            migrationBuilder.CreateIndex(
                name: "IX_DividendPayments_UserId_AccountId_PaymentDate_Id",
                table: "DividendPayments",
                columns: new[] { "UserId", "AccountId", "PaymentDate", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DividendPayments");
        }
    }
}