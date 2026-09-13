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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AlertType = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ComparisonOperator = table.Column<int>(type: "integer", nullable: false),
                    Threshold = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EvaluationPeriod = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<int>(type: "integer", nullable: true),
                    LabelId = table.Column<int>(type: "integer", nullable: true),
                    LabelName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MerchantName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastStatus = table.Column<int>(type: "integer", nullable: false),
                    LastTriggeredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastTriggeredValue = table.Column<decimal>(type: "numeric", nullable: true),
                    LastTriggeredConditionFingerprint = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CooldownPeriod = table.Column<TimeSpan>(type: "interval", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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