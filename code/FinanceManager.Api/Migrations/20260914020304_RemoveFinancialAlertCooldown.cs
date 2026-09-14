using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFinancialAlertCooldown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CooldownPeriod",
                table: "FinancialAlerts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "CooldownPeriod",
                table: "FinancialAlerts",
                type: "interval",
                nullable: true);
        }
    }
}