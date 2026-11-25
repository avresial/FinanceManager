# 0008 — Owner-keyed transaction ledger

Accepted on 2026-11-22 in epic #1126. Implemented on 2026-11-25: the schema contract of step 13c (#1144) completed it. This decision supersedes [ADR 0006](0006-account-models-without-inheritance.md).

## Context

People record cash movements, bonds and trades on the Transactions page. Requiring a named financial account added setup and split holdings in the same instrument across multiple balances. Editing an old entry also rewrote later running balances.

## Decision

Keep the three entry tables. Each row carries its owner. Cash pools by `(UserId, CurrencyId)`, bonds by `(UserId, BondDetailsId)` and trades by `(UserId, AssetListingId)`. Cash and bond balances use `SUM(ValueChange)`. Investment holdings sum signed quantities. A range query groups opening balances and walks the ordered changes inside the range. Dividends are separate owner-keyed income records.

## Alternatives

- Stored balances make point reads cheaper but require serialized suffix updates after backdated edits. Revisit this if grouped reads exceed the latency budget at the plan limit.
- A single polymorphic table could simplify frequent cross-kind database operations. It would require sparse columns, mixed precision and a larger migration.
- A transaction-source table or labels could retain bank and broker names. The product drops account names. Global financial labels also participate in analytics and automation.

## Consequences

The authenticated principal determines ownership, including for guest users. Writes affect the selected row and invalidate owner caches. Net cash in each currency contributes to assets when positive and liabilities when negative. Bond accrual starts at the latest pooled entry. Existing FX and cost-basis policies continue.

`DropFinancialAccounts` (#1144) dropped the `Accounts` table, the historical account links and the stored `Value` columns, without an archive. The pre-contract backup is the only rollback ([runbook](../operations/migrations.md#financial-accounts-contract-1144)). The label join table and columns that still contain `Account` keep their physical names through explicit EF mappings (#1143). See [epic #1126](https://github.com/avresial/FinanceManager/issues/1126).
