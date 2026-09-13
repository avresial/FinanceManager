using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinancialAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    UserId = table.Column<int>(nullable: false),
                    Title = table.Column<string>(maxLength: 200, nullable: false),
                    AlertType = table.Column<int>(nullable: false),
                    IsEnabled = table.Column<bool>(nullable: false),
                    ComparisonOperator = table.Column<int>(nullable: false),
                    Threshold = table.Column<decimal>(precision: 18, scale: 2, nullable: false),
                    EvaluationPeriod = table.Column<int>(nullable: false),
                    AccountId = table.Column<int>(nullable: true),
                    LabelId = table.Column<int>(nullable: true),
                    LabelName = table.Column<string>(maxLength: 200, nullable: true),
                    MerchantName = table.Column<string>(maxLength: 200, nullable: true),
                    SubscriptionId = table.Column<Guid>(nullable: true),
                    LastStatus = table.Column<int>(nullable: false),
                    LastTriggeredAt = table.Column<DateTime>(nullable: true),
                    LastTriggeredValue = table.Column<decimal>(nullable: true),
                    LastTriggeredConditionFingerprint = table.Column<string>(maxLength: 1000, nullable: true),
                    CooldownPeriod = table.Column<TimeSpan>(nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialAlerts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAlerts_UserId_AlertType",
                table: "FinancialAlerts",
                columns: new[] { "UserId", "AlertType" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAlerts_UserId_IsEnabled",
                table: "FinancialAlerts",
                columns: new[] { "UserId", "IsEnabled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinancialAlerts");
        }
    }
}