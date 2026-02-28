using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddedCsvImportHeaderMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CsvHeaderMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HeaderName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FieldName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CsvHeaderMappings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CsvHeaderMapping_Header",
                table: "CsvHeaderMappings",
                column: "HeaderName");

            migrationBuilder.CreateIndex(
                name: "IX_CsvHeaderMapping_Header_Field",
                table: "CsvHeaderMappings",
                columns: new[] { "HeaderName", "FieldName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CsvHeaderMappings");
        }
    }
}
