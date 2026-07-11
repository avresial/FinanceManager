using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddShareClassFigiUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Assets_ShareClassFigi",
                table: "Assets",
                column: "ShareClassFigi",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assets_ShareClassFigi",
                table: "Assets");
        }
    }
}