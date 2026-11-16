# 0007 — Shared API contracts

Status: accepted and implemented for the core financial-account slice, 2026-11-16.

## Context

Account wire DTOs lived in Domain and exposed domain labels and enums. The API and Blazor client both need the same payload definitions, while domain behavior should evolve independently of those payloads. The user approved a shared Contracts project in [#1111](https://github.com/avresial/FinanceManager/issues/1111); the bounded plan was reviewed by Claude Opus before implementation.

## Decision

Add independent [FinanceManager.Contracts](../../../code/FinanceManager.Contracts) for core currency/bond account and entry responses, investment transaction responses and Add/Update requests, history and valuation responses. Keep existing type names and JSON shapes. Explicitly numbered transport enums and label/classification DTOs prevent references to Domain. Numeric casts at mapping boundaries retain existing behavior for undefined enum values; parity tests pin defined names and numbers.

Application and presentation consume Contracts. Domain and Infrastructure types must not depend on it. Investment mappings and the transaction-valuation service interface belong in Application; currency/bond HTTP response mappings belong in API. Browser clients retain their existing local account models through explicit mapping. Domain grants Application internal access to retain transaction identity mapping without making the identity setter public.

## Compatibility and scope

Preserve numeric enums, property order, optional defaults, DateTime posting dates, DateOnly trade dates, long identifiers, nulls, classification shapes and browser snapshot readability. Preserve the existing currency/bond response default `accountType: 0`, entry-label classification omission, and carried bond-entry label omission. No financial semantics, routes, schema or migrations change.

The persisted `FinancialAccountBaseDto` row and domain `AddCurrencyEntryDto` command are not wire contracts. Imports, dividends, fee-drag, exports and other feature contracts remain for separate slices because their domain repository/service boundaries need separate decisions. No empty compatibility wrappers are introduced.

## Alternatives

- DTOs in Application avoid another project and fit internal use-case results; they make shared HTTP consumers depend on the application assembly. Contracts gives the API/client an independent transport boundary.
- API-owned DTOs fit generated or separately maintained clients. Prefer this when the browser no longer shares .NET wire definitions directly.
- Domain-owned payloads avoid mappings but couple business models to JSON and client evolution. Keep domain commands and business read models there, rather than extending this convention to new wire models.

## Consequences and verification

Mappings add small explicit conversions and duplicate enum definitions; tests enforce numeric/name parity, JSON compatibility and dependency rules. The extraction is incremental rather than a claim that every existing API is transport-independent. Existing compiled-assembly architecture checks now include Contracts and prohibit Domain/Infrastructure type dependencies on it. Both-provider schema checks and full solution tests protect compatibility within their documented coverage.
