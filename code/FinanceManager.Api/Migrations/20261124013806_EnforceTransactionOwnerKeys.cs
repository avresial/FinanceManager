using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations;

public partial class EnforceTransactionOwnerKeys : Migration
{
    private bool IsSqlServer => ActiveProvider switch
    {
        "Npgsql.EntityFrameworkCore.PostgreSQL" => false,
        "Microsoft.EntityFrameworkCore.SqlServer" => true,
        _ => throw new NotSupportedException($"Unsupported owner-key migration provider: {ActiveProvider}."),
    };

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(IsSqlServer
            ? """
              UPDATE e SET [UserId] = COALESCE(e.[UserId], a.[UserId]), [CurrencyId] = COALESCE(e.[CurrencyId], a.[CurrencyId])
              FROM [CurrencyEntries] e JOIN [Accounts] a ON a.[AccountId] = e.[AccountId]
              WHERE a.[AccountType] = 0 AND a.[UserId] <> 0 AND (e.[UserId] IS NULL OR e.[CurrencyId] IS NULL);

              UPDATE e SET [UserId] = a.[UserId]
              FROM [BondEntries] e JOIN [Accounts] a ON a.[AccountId] = e.[AccountId]
              WHERE a.[AccountType] = 2 AND a.[UserId] <> 0 AND e.[UserId] IS NULL;
              """
            : """
              UPDATE "CurrencyEntries" e SET "UserId" = COALESCE(e."UserId", a."UserId"), "CurrencyId" = COALESCE(e."CurrencyId", a."CurrencyId")
              FROM "Accounts" a WHERE a."AccountId" = e."AccountId" AND a."AccountType" = 0 AND a."UserId" <> 0
                AND (e."UserId" IS NULL OR e."CurrencyId" IS NULL);

              UPDATE "BondEntries" e SET "UserId" = a."UserId"
              FROM "Accounts" a WHERE a."AccountId" = e."AccountId" AND a."AccountType" = 2 AND a."UserId" <> 0 AND e."UserId" IS NULL;
              """);

        // InvestmentTransaction.EntryId maps to Id. Its owner is bigint, including when it has no Users row.
        var count = IsSqlServer ? "COUNT_BIG" : "COUNT";
        var inventory = $"""
            WITH entries(kind, entry_id, account_id, owner_id, currency_id) AS (
                SELECT 0, "EntryId", "AccountId", CAST("UserId" AS bigint), "CurrencyId" FROM "CurrencyEntries"
                UNION ALL SELECT 2, "EntryId", "AccountId", CAST("UserId" AS bigint), NULL FROM "BondEntries"
                UNION ALL SELECT 1, "Id", "AccountId", CAST("UserId" AS bigint), NULL FROM "InvestmentTransactions"
                UNION ALL SELECT 1, "Id", "AccountId", CAST("UserId" AS bigint), NULL FROM "DividendPayments"
            ), inventory AS (
                SELECT {count}(CASE WHEN e.owner_id IS NULL OR e.owner_id = 0 OR (e.kind = 0 AND e.currency_id IS NULL) THEN 1 END) AS null_or_zero_keys,
                       {count}(CASE WHEN e.kind = 0 AND e.currency_id IS NOT NULL AND
                             (c."Id" IS NULL OR (e.currency_id = 0 AND UPPER(c."ShortName") <> 'PLN')) THEN 1 END) AS invalid_cash_currency,
                       {count}(CASE WHEN a."AccountId" IS NOT NULL AND
                             (a."AccountType" <> e.kind OR a."UserId" = 0 OR a."UserId" <> e.owner_id
                              OR (e.kind = 0 AND a."CurrencyId" <> e.currency_id)) THEN 1 END) AS account_mismatch,
                       (SELECT {count}(*) FROM "FinancialAlerts" WHERE "AccountId" IS NOT NULL) AS legacy_alert_scope
                FROM entries e
                LEFT JOIN "Accounts" a ON a."AccountId" = e.account_id
                LEFT JOIN "Currencies" c ON c."Id" = e.currency_id
            )
            """;

        migrationBuilder.Sql(IsSqlServer
            ? $"""
               SET QUOTED_IDENTIFIER ON;
               DECLARE @keys bigint, @currencies bigint, @accounts bigint, @alerts bigint;
               {inventory}
               SELECT @keys = null_or_zero_keys, @currencies = invalid_cash_currency,
                      @accounts = account_mismatch, @alerts = legacy_alert_scope FROM inventory;
               IF @keys <> 0 OR @currencies <> 0 OR @accounts <> 0 OR @alerts <> 0
               BEGIN
                   DECLARE @msg nvarchar(2048) = CONCAT('Owner-key cutover failed: null_or_zero_keys=', @keys,
                       ', invalid_cash_currency=', @currencies, ', account_mismatch=', @accounts, ', legacy_alert_scope=', @alerts,
                       '. Inspect the cutover inventory in docs/architecture/operations/migrations.md and repair the reported rows before retrying.');
                   THROW 50000, @msg, 1;
               END;
               """
            : $"""
               DO $cutover$
               DECLARE issues record;
               BEGIN
                   {inventory}
                   SELECT * INTO issues FROM inventory;
                   IF issues.null_or_zero_keys <> 0 OR issues.invalid_cash_currency <> 0 OR issues.account_mismatch <> 0 OR issues.legacy_alert_scope <> 0 THEN
                       RAISE EXCEPTION 'Owner-key cutover failed: null_or_zero_keys=%, invalid_cash_currency=%, account_mismatch=%, legacy_alert_scope=%. Inspect the cutover inventory in docs/architecture/operations/migrations.md and repair the reported rows before retrying.',
                           issues.null_or_zero_keys, issues.invalid_cash_currency, issues.account_mismatch, issues.legacy_alert_scope;
                   END IF;
               END $cutover$;
               """);

        SetKeyNullability(migrationBuilder, nullable: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => SetKeyNullability(migrationBuilder, nullable: true);

    private void SetKeyNullability(MigrationBuilder migrationBuilder, bool nullable)
    {
        (string Table, string Name, string[] Columns)[] indexes =
        [
            ("CurrencyEntries", "IX_CurrencyEntries_OwnerDateId", ["UserId", "PostingDate", "EntryId"]),
            ("CurrencyEntries", "IX_CurrencyEntries_OwnerCurrencyDateId", ["UserId", "CurrencyId", "PostingDate", "EntryId"]),
            ("BondEntries", "IX_BondEntries_OwnerDateId", ["UserId", "PostingDate", "EntryId"]),
            ("BondEntries", "IX_BondEntries_OwnerBondDateId", ["UserId", "BondDetailsId", "PostingDate", "EntryId"]),
        ];

        if (IsSqlServer)
            foreach (var index in indexes)
                migrationBuilder.DropIndex(index.Name, index.Table);

        var type = IsSqlServer ? "int" : "integer";
        foreach (var (table, column) in new[] { ("CurrencyEntries", "CurrencyId"), ("CurrencyEntries", "UserId"), ("BondEntries", "UserId") })
            migrationBuilder.AlterColumn<int>(column, table, type: type, nullable: nullable,
                oldClrType: typeof(int), oldType: type, oldNullable: !nullable);

        if (IsSqlServer)
            foreach (var index in indexes)
                migrationBuilder.CreateIndex(index.Name, index.Table, index.Columns);
    }
}