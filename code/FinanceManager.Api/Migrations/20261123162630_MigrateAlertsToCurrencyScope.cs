using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class MigrateAlertsToCurrencyScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                table: "FinancialAlerts",
                type: "integer",
                nullable: true);

            // Alerts stop being scoped to a financial account: the four cash alert types (1 AccountBalance, 2 CategorySpending,
            // 3 MerchantSpending, 4 LargeTransaction) now compare native amounts in one currency over all of the owner's transactions.
            // Their meaning changes (an unscoped balance alert used to fire on any single account, now on the pooled currency balance),
            // so a stored trigger or fingerprint would not carry over. The migration therefore:
            //   1. prefills CurrencyId only where it is exact: the alert had an account and that account is a Currency account of the
            //      same user. Alerts without an account, or whose account is gone or of another kind, stay without a currency;
            //   2. disables all four cash types and resets their trigger state (Healthy, no fingerprint), so the user reviews the
            //      currency and re-enables them, and the first evaluation after that is a normal fresh trigger;
            //   3. clears the legacy AccountId on every alert. SubscriptionPriceChange (5) alerts never used it and stay enabled.
            // Nothing is guessed, no row can fail the statements, and a second run changes nothing. The AccountId column stays until the
            // contract step. Statuses: 1 = Healthy.
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
                migrationBuilder.Sql("""
                    UPDATE "FinancialAlerts" AS fa
                    SET "CurrencyId" = (
                        SELECT a."CurrencyId"
                        FROM "Accounts" AS a
                        WHERE a."AccountId" = fa."AccountId" AND a."UserId" = fa."UserId" AND a."AccountType" = 0
                    )
                    WHERE fa."AccountId" IS NOT NULL
                      AND fa."CurrencyId" IS NULL
                      AND fa."AlertType" IN (1, 2, 3, 4);

                    UPDATE "FinancialAlerts"
                    SET "IsEnabled" = FALSE,
                        "LastStatus" = 1,
                        "LastTriggeredConditionFingerprint" = NULL,
                        "UpdatedAt" = CURRENT_TIMESTAMP
                    WHERE "AlertType" IN (1, 2, 3, 4)
                      AND ("IsEnabled" = TRUE OR "LastStatus" <> 1 OR "LastTriggeredConditionFingerprint" IS NOT NULL);

                    UPDATE "FinancialAlerts"
                    SET "AccountId" = NULL
                    WHERE "AccountId" IS NOT NULL;
                    """);
            else if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
                migrationBuilder.Sql("""
                    UPDATE fa
                    SET fa.[CurrencyId] = (
                        SELECT a.[CurrencyId]
                        FROM [Accounts] AS a
                        WHERE a.[AccountId] = fa.[AccountId] AND a.[UserId] = fa.[UserId] AND a.[AccountType] = 0
                    )
                    FROM [FinancialAlerts] AS fa
                    WHERE fa.[AccountId] IS NOT NULL
                      AND fa.[CurrencyId] IS NULL
                      AND fa.[AlertType] IN (1, 2, 3, 4);

                    UPDATE [FinancialAlerts]
                    SET [IsEnabled] = 0,
                        [LastStatus] = 1,
                        [LastTriggeredConditionFingerprint] = NULL,
                        [UpdatedAt] = SYSUTCDATETIME()
                    WHERE [AlertType] IN (1, 2, 3, 4)
                      AND ([IsEnabled] = 1 OR [LastStatus] <> 1 OR [LastTriggeredConditionFingerprint] IS NOT NULL);

                    UPDATE [FinancialAlerts]
                    SET [AccountId] = NULL
                    WHERE [AccountId] IS NOT NULL;
                    """);
            else
                throw new NotSupportedException($"Unsupported migration provider: {ActiveProvider}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the column is reverted: the disabled alerts, reset trigger state and cleared AccountId scopes need a backup restore.
            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "FinancialAlerts");
        }
    }
}