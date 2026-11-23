using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class RetireTransactionRuleAccountConditions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The Account condition is retired with the accounts themselves. A rule that carried one loses it and is disabled, so it is
            // reviewed before it runs again: without the condition it can match more transactions than the user ever configured (a rule
            // whose only condition was an account now has none and would match every cash transaction). Other conditions keep their order,
            // rules without an Account condition are not touched, and a second run changes nothing. The condition type is matched
            // case-insensitively after trimming, as the application serialised it; anything more exotic that survives is never matched at runtime.
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
                migrationBuilder.Sql("""
                    UPDATE "TransactionRules" AS r
                    SET "ConditionsJson" = COALESCE(
                            (
                                SELECT jsonb_agg(e.elem ORDER BY e.ord)::text
                                FROM jsonb_array_elements(c.arr) WITH ORDINALITY AS e(elem, ord)
                                WHERE NOT (
                                    jsonb_typeof(e.elem) = 'object'
                                    AND LOWER(BTRIM(COALESCE(e.elem->>'type', e.elem->>'Type', ''))) = 'account'
                                )
                            ),
                            '[]'
                        ),
                        "IsEnabled" = FALSE,
                        "UpdatedAt" = CURRENT_TIMESTAMP
                    FROM (
                        SELECT "Id",
                               CASE WHEN jsonb_typeof("ConditionsJson"::jsonb) = 'array'
                                    THEN "ConditionsJson"::jsonb
                                    ELSE '[]'::jsonb END AS arr
                        FROM "TransactionRules"
                    ) AS c
                    WHERE c."Id" = r."Id"
                      AND EXISTS (
                          SELECT 1
                          FROM jsonb_array_elements(c.arr) AS x(elem)
                          WHERE jsonb_typeof(x.elem) = 'object'
                            AND LOWER(BTRIM(COALESCE(x.elem->>'type', x.elem->>'Type', ''))) = 'account'
                      );
                    """);
            else if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
                migrationBuilder.Sql("""
                    UPDATE r
                    SET r.[ConditionsJson] = COALESCE(
                            N'[' + (
                                SELECT STRING_AGG(CAST(e.[value] AS nvarchar(max)), N',')
                                       WITHIN GROUP (ORDER BY CAST(e.[key] AS int))
                                FROM OPENJSON(r.[ConditionsJson]) AS e
                                WHERE NOT (
                                    e.[type] = 5
                                    AND LOWER(LTRIM(RTRIM(COALESCE(
                                        JSON_VALUE(e.[value], '$.type'),
                                        JSON_VALUE(e.[value], '$.Type'),
                                        N'')))) = N'account'
                                )
                            ) + N']',
                            N'[]'
                        ),
                        r.[IsEnabled] = 0,
                        r.[UpdatedAt] = SYSUTCDATETIME()
                    FROM [TransactionRules] AS r
                    WHERE ISJSON(r.[ConditionsJson]) = 1
                      AND LEFT(LTRIM(r.[ConditionsJson]), 1) = N'['
                      AND EXISTS (
                          SELECT 1
                          FROM OPENJSON(r.[ConditionsJson]) AS e
                          WHERE e.[type] = 5
                            AND LOWER(LTRIM(RTRIM(COALESCE(
                                JSON_VALUE(e.[value], '$.type'),
                                JSON_VALUE(e.[value], '$.Type'),
                                N'')))) = N'account'
                      );
                    """);
            else
                throw new NotSupportedException($"Unsupported migration provider: {ActiveProvider}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only: the removed account ids and the previous enabled flags cannot be reconstructed.
            // Roll back from the pre-deploy backup (docs/architecture/operations/migrations.md).
        }
    }
}