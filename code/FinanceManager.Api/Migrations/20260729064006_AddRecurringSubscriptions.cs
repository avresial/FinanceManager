using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecurringSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    UserId = table.Column<int>(nullable: false),
                    MerchantKey = table.Column<string>(maxLength: 300, nullable: false),
                    Name = table.Column<string>(maxLength: 300, nullable: false),
                    IsMuted = table.Column<bool>(nullable: false),
                    IsCancelled = table.Column<bool>(nullable: false),
                    IsFlaggedForReview = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringSubscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringSubscriptions_UserId_MerchantKey",
                table: "RecurringSubscriptions",
                columns: new[] { "UserId", "MerchantKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecurringSubscriptions");
        }
    }
}