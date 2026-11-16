# 0006 — Account models without implementation inheritance

[Architecture decisions](../09-architecture-decisions.md)

Status: accepted for implementation, 2026-11-16. The developer requested removal of account-model base classes; [#1109](https://github.com/avresial/FinanceManager/issues/1109) records the reviewed plan. This record does not assert deployment.

## Context

Currency accounts have one balance stream, bonds have one per instrument, and investments maintain quantities per account/listing through their repository. Shared state and balance behavior in base classes blurred those differences. The prior ledger refactor removed the account behavior base; this change removes the remaining account, entry and response DTO parents.

## Decision

Concrete account and transaction models own their properties. [IAccount](../../../code/FinanceManager.Domain/FinancialAccounts/Shared/Entities/IAccount.cs) supplies only account identity, ownership and name to generic repository/client consumers. Currency and bond history dates belong to their concrete models. The investment account retains its null date properties for wire compatibility without placing them on IAccount.

[ILedgerEntry](../../../code/FinanceManager.Domain/FinancialAccounts/Shared/Entities/ILedgerEntry.cs) supplies dated identity, account identity, a running balance and a signed change to existing shared ledger and cache operations. Only currency and bond entries implement it. Each concrete type owns mutation, copying and financial semantics; investment transactions keep their date normalization, quantity-derived change and repository-managed balance.

Account/entry response DTOs are flat and preserve existing property names, types and JSON order. FinancialAccountBaseDto keeps its CLR/model identity as a standalone persisted Accounts row. EF table/column mappings, backing-field access, precision and indexes remain compatible; this refactor requires no schema migration. Existing rules and known bugs #1077 and #1107 are outside its scope.

## Alternatives and consequences

Keeping the base classes would share property declarations but continue coupling unrelated financial models. A property mixin or shared state object would reduce those declarations at the cost of forwarding and more involved persistence/serialization mappings. Small duplicated properties and concrete update code are preferable here.

A union is appropriate when a consumer handles a closed set of concrete account or transaction alternatives. Current transaction logs already project type-specific streams to common DTOs, so adding a union would introduce an unused abstraction. Introduce one when a real consumer needs exhaustive case dispatch. Interfaces are preferable for existing generic consumers that only require common capabilities.

This change deliberately preserves copy/label handling, public setters, date materialization behavior and exact payloads. Tests cover standalone model structure, JSON compatibility, field access and both relational models; the implementation also compares full provider schemas and create scripts against the pre-refactor baseline. These checks do not prove production database deployment state.
