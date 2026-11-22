using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class ExpandTransactionOwnerKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var sqlServer = ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer";
            var integerType = sqlServer ? "int" : "integer";

            migrationBuilder.DropForeignKey(
                name: "FK_DividendPayments_Accounts_AccountId",
                table: "DividendPayments");

            if (sqlServer) DropAccountIndexes(migrationBuilder);

            migrationBuilder.AlterColumn<int>(
                name: "AccountId",
                table: "InvestmentTransactions",
                type: integerType,
                nullable: true,
                oldClrType: typeof(int),
                oldType: integerType);

            migrationBuilder.AlterColumn<int>(
                name: "AccountId",
                table: "DividendPayments",
                type: integerType,
                nullable: true,
                oldClrType: typeof(int),
                oldType: integerType);

            migrationBuilder.AlterColumn<int>(
                name: "AccountId",
                table: "CurrencyEntries",
                type: integerType,
                nullable: true,
                oldClrType: typeof(int),
                oldType: integerType);

            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                table: "CurrencyEntries",
                type: integerType,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "CurrencyEntries",
                type: integerType,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AccountId",
                table: "BondEntries",
                type: integerType,
                nullable: true,
                oldClrType: typeof(int),
                oldType: integerType);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "BondEntries",
                type: integerType,
                nullable: true);

            // SQL Server compiles the generated script as one batch. Bind new columns after ALTER TABLE.
            migrationBuilder.Sql(sqlServer
                ? """
                  EXEC(N'UPDATE e SET [UserId] = a.[UserId], [CurrencyId] = a.[CurrencyId]
                  FROM [CurrencyEntries] AS e JOIN [Accounts] AS a ON e.[AccountId] = a.[AccountId]
                  WHERE a.[AccountType] = 0 AND a.[UserId] <> 0;');

                  EXEC(N'UPDATE e SET [UserId] = a.[UserId]
                  FROM [BondEntries] AS e JOIN [Accounts] AS a ON e.[AccountId] = a.[AccountId]
                  WHERE a.[AccountType] = 2 AND a.[UserId] <> 0;');
                  """
                : """
                  UPDATE "CurrencyEntries" AS e SET "UserId" = a."UserId", "CurrencyId" = a."CurrencyId"
                  FROM "Accounts" AS a WHERE e."AccountId" = a."AccountId" AND a."AccountType" = 0 AND a."UserId" <> 0;

                  UPDATE "BondEntries" AS e SET "UserId" = a."UserId"
                  FROM "Accounts" AS a WHERE e."AccountId" = a."AccountId" AND a."AccountType" = 2 AND a."UserId" <> 0;
                  """);

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentTransactions_OwnerListingDateId",
                table: "InvestmentTransactions",
                columns: new[] { "UserId", "AssetListingId", "PostingDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DividendPayments_OwnerPaymentDateId",
                table: "DividendPayments",
                columns: new[] { "UserId", "PaymentDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyEntries_OwnerCurrencyDateId",
                table: "CurrencyEntries",
                columns: new[] { "UserId", "CurrencyId", "PostingDate", "EntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_BondEntries_OwnerBondDateId",
                table: "BondEntries",
                columns: new[] { "UserId", "BondDetailsId", "PostingDate", "EntryId" });

            if (sqlServer) CreateAccountIndexes(migrationBuilder);

            migrationBuilder.AddForeignKey(
                name: "FK_DividendPayments_Accounts_AccountId",
                table: "DividendPayments",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "AccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var sqlServer = ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer";
            var integerType = sqlServer ? "int" : "integer";

            // Once account-free writes begin, restoring the previous schema needs a backup.
            migrationBuilder.Sql(sqlServer
                ? """
                  IF EXISTS (SELECT 1 FROM [CurrencyEntries] WHERE [AccountId] IS NULL)
                    OR EXISTS (SELECT 1 FROM [BondEntries] WHERE [AccountId] IS NULL)
                    OR EXISTS (SELECT 1 FROM [InvestmentTransactions] WHERE [AccountId] IS NULL)
                    OR EXISTS (SELECT 1 FROM [DividendPayments] WHERE [AccountId] IS NULL)
                      THROW 50000, 'Cannot roll back while unlinked ledger rows exist.', 1;
                  """
                : """
                  DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "CurrencyEntries" WHERE "AccountId" IS NULL)
                      OR EXISTS (SELECT 1 FROM "BondEntries" WHERE "AccountId" IS NULL)
                      OR EXISTS (SELECT 1 FROM "InvestmentTransactions" WHERE "AccountId" IS NULL)
                      OR EXISTS (SELECT 1 FROM "DividendPayments" WHERE "AccountId" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot roll back while unlinked ledger rows exist.';
                    END IF;
                  END $$;
                  """);

            migrationBuilder.DropForeignKey(
                name: "FK_DividendPayments_Accounts_AccountId",
                table: "DividendPayments");

            if (sqlServer) DropAccountIndexes(migrationBuilder);

            migrationBuilder.DropIndex(
                name: "IX_InvestmentTransactions_OwnerListingDateId",
                table: "InvestmentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_DividendPayments_OwnerPaymentDateId",
                table: "DividendPayments");

            migrationBuilder.DropIndex(
                name: "IX_CurrencyEntries_OwnerCurrencyDateId",
                table: "CurrencyEntries");

            migrationBuilder.DropIndex(
                name: "IX_BondEntries_OwnerBondDateId",
                table: "BondEntries");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "CurrencyEntries");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "CurrencyEntries");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "BondEntries");

            migrationBuilder.AlterColumn<int>(
                name: "AccountId",
                table: "InvestmentTransactions",
                type: integerType,
                nullable: false,
                oldClrType: typeof(int),
                oldType: integerType,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AccountId",
                table: "DividendPayments",
                type: integerType,
                nullable: false,
                oldClrType: typeof(int),
                oldType: integerType,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AccountId",
                table: "CurrencyEntries",
                type: integerType,
                nullable: false,
                oldClrType: typeof(int),
                oldType: integerType,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AccountId",
                table: "BondEntries",
                type: integerType,
                nullable: false,
                oldClrType: typeof(int),
                oldType: integerType,
                oldNullable: true);

            if (sqlServer) CreateAccountIndexes(migrationBuilder);

            migrationBuilder.AddForeignKey(
                name: "FK_DividendPayments_Accounts_AccountId",
                table: "DividendPayments",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "AccountId",
                onDelete: ReferentialAction.Cascade);
        }

        // SQL Server requires every index containing AccountId to be removed before ALTER COLUMN.
        private static readonly (string Table, string[] Columns)[] _accountIndexes =
        [
            ("BondEntries", ["AccountId", "PostingDate", "EntryId"]),
            ("BondEntries", ["AccountId", "BondDetailsId", "PostingDate", "EntryId"]),
            ("CurrencyEntries", ["AccountId", "PostingDate", "EntryId"]),
            ("InvestmentTransactions", ["AccountId"]),
            ("InvestmentTransactions", ["AccountId", "PostingDate", "Id"]),
            ("DividendPayments", ["AccountId"]),
            ("DividendPayments", ["UserId", "AccountId", "PaymentDate", "Id"])
        ];

        private static void DropAccountIndexes(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, columns) in _accountIndexes)
                migrationBuilder.DropIndex($"IX_{table}_{string.Join('_', columns)}", table);
        }

        private static void CreateAccountIndexes(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, columns) in _accountIndexes)
                migrationBuilder.CreateIndex($"IX_{table}_{string.Join('_', columns)}", table, columns);
        }
    }
}