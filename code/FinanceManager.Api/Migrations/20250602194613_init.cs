using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations;

/// <inheritdoc />
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
public partial class init : Migration
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ActiveUsers",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<int>(type: "int", nullable: false),
                LoginTime = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ActiveUsers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "BankEntries",
            columns: table => new
            {
                EntryId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ExpenseType = table.Column<int>(type: "int", nullable: false),
                AccountId = table.Column<int>(type: "int", nullable: false),
                PostingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                ValueChange = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BankEntries", x => x.EntryId);
            });

        migrationBuilder.CreateTable(
            name: "FinancialAccountBaseDto",
            columns: table => new
            {
                AccountId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                AccountType = table.Column<int>(type: "int", nullable: false),
                AccountLabel = table.Column<int>(type: "int", nullable: false),
                UserId = table.Column<int>(type: "int", nullable: false),
                Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FinancialAccountBaseDto", x => x.AccountId);
            });

        migrationBuilder.CreateTable(
            name: "NewVisits",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                DateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                VisitsCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NewVisits", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "StockEntries",
            columns: table => new
            {
                EntryId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Ticker = table.Column<string>(type: "nvarchar(max)", nullable: false),
                InvestmentType = table.Column<int>(type: "int", nullable: false),
                AccountId = table.Column<int>(type: "int", nullable: false),
                PostingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                ValueChange = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StockEntries", x => x.EntryId);
            });

        migrationBuilder.CreateTable(
            name: "StockPrices",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Ticker = table.Column<string>(type: "nvarchar(max)", nullable: false),
                PricePerUnit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Date = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StockPrices", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Login = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                PricingLevel = table.Column<int>(type: "int", nullable: false),
                UserRole = table.Column<int>(type: "int", nullable: false),
                CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ActiveUsers");

        migrationBuilder.DropTable(
            name: "BankEntries");

        migrationBuilder.DropTable(
            name: "FinancialAccountBaseDto");

        migrationBuilder.DropTable(
            name: "NewVisits");

        migrationBuilder.DropTable(
            name: "StockEntries");

        migrationBuilder.DropTable(
            name: "StockPrices");

        migrationBuilder.DropTable(
            name: "Users");
    }
}