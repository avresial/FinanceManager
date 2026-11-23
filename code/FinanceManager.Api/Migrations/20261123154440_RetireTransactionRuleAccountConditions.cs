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
            // case-insensitively after trimming, as the application serialised it.
            // ConditionsJson is plain text, so a deploy must never fail on a row that is not valid JSON: only rows that mention "account"
            // are parsed at all, and a row that cannot be parsed is skipped. Skipped (or exotically escaped) rows keep their conditions and
            // are never matched at runtime (TransactionRuleRuntimeBuilder disables rules that still carry an Account condition).
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
                // A per-row block with an exception handler works on every PostgreSQL version (pg_input_is_valid needs 16+) and cannot be
                // defeated by the planner evaluating the jsonb cast early. The $retire$ tag avoids colliding with Npgsql's $EF$ wrapper.
                migrationBuilder.Sql("""
                    DO $retire$
                    DECLARE candidate RECORD;
                    BEGIN
                        FOR candidate IN
                            SELECT "Id", "ConditionsJson"
                            FROM "TransactionRules"
                            WHERE "ConditionsJson" ILIKE '%account%'
                        LOOP
                            BEGIN
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
                                    SELECT CASE WHEN jsonb_typeof(candidate."ConditionsJson"::jsonb) = 'array'
                                                THEN candidate."ConditionsJson"::jsonb
                                                ELSE '[]'::jsonb END AS arr
                                ) AS c
                                WHERE r."Id" = candidate."Id"
                                  AND EXISTS (
                                      SELECT 1
                                      FROM jsonb_array_elements(c.arr) AS x(elem)
                                      WHERE jsonb_typeof(x.elem) = 'object'
                                        AND LOWER(BTRIM(COALESCE(x.elem->>'type', x.elem->>'Type', ''))) = 'account'
                                  );
                            EXCEPTION
                                -- Invalid JSON (22P02), \u0000 escapes (22P05) and other data errors; nesting too deep to parse.
                                WHEN data_exception OR program_limit_exceeded THEN
                                    NULL;
                            END;
                        END LOOP;
                    END
                    $retire$;
                    """);
            else if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                // SQL Server may evaluate WHERE predicates in any order, so ISJSON alone cannot protect OPENJSON (error 13609 on invalid
                // text). OPENJSON(NULL) yields no rows under every plan, so the guard sits inside the argument instead.
                const string conditions = "OPENJSON(CASE WHEN ISJSON(r.[ConditionsJson]) = 1 AND LEFT(LTRIM(r.[ConditionsJson]), 1) = N'[' THEN r.[ConditionsJson] END)";
                migrationBuilder.Sql($"""
                    UPDATE r
                    SET r.[ConditionsJson] = COALESCE(
                            N'[' + (
                                SELECT STRING_AGG(CAST(e.[value] AS nvarchar(max)), N',')
                                       WITHIN GROUP (ORDER BY CAST(e.[key] AS int))
                                FROM {conditions} AS e
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
                    WHERE r.[ConditionsJson] LIKE N'%account%'
                      AND EXISTS (
                          SELECT 1
                          FROM {conditions} AS e
                          WHERE e.[type] = 5
                            AND LOWER(LTRIM(RTRIM(COALESCE(
                                JSON_VALUE(e.[value], '$.type'),
                                JSON_VALUE(e.[value], '$.Type'),
                                N'')))) = N'account'
                      );
                    """);
            }
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