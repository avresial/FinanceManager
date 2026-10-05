# ADR 0002: Compose external providers behind ordered fallback contracts

[Decision index](../09-architecture-decisions.md) · [Architecture index](../README.md)

- **Status:** Reconstructed / implemented
- **Documented:** 2026-10-05
- **Original approval/date:** Not established by this record

## Context and observed decision

Financial quotes, exchange rates and AI output depend on providers with different capabilities, symbol conventions, quotas and failure modes. Feature orchestration needs domain/application contracts rather than provider-specific HTTP logic.

Infrastructure registers adapters while dedicated coordinators implement selection/fallback. [FallbackStockPriceSource](../../../code/FinanceManager.Application/FinancialAccounts/Stock/Pricing/FallbackStockPriceSource.cs) orders registered price sources by priority, returns the first nonempty result, continues after errors/timeouts and propagates caller cancellation. Its explicit provider-selection path preserves requested-provider semantics. [Infrastructure composition](../../../code/FinanceManager.Infrastructure/ServiceCollectionExtension.cs) supplies Alpha Vantage, Twelve Data and EODHD, as well as ordered official/general FX adapters. [AI composition](../../../code/FinanceManager.Infrastructure/Shared/Ai/ServiceCollectionExtension.cs) registers LM Studio, OpenRouter, Copilot/GitHub Models and Ollama; [FallbackChatClient](../../../code/FinanceManager.Infrastructure/Shared/Ai/FallbackChatClient.cs) resolves configured provider/model attempts rather than enforcing one universal hardcoded order.

The decision represented by the code is **contract-based composition with explicit outcome and cancellation semantics**. The [integration guide](../concepts/integrations.md) supplies provider/configuration detail; [ServiceDefaults](../../../code/ServiceDefaults/Extensions.cs) supplies common HTTP resilience separately.

## Consequences and present-day assessment

Consumers can reuse one capability while providers evolve. Failures or entitlement gaps need not immediately fail every user request. Typed result/provenance can distinguish missing, unpublished and exact/carried data.

Fallback can multiply latency/requests and spend provider quotas. A first nonempty response is not proof of complete history or equivalent provider quality. Provider capability/symbol mapping and configured priorities remain important. Cancellation must stop work; unsafe operation retries are disabled by the common resilience defaults. AI exhausted attempts can throw aggregate failures or yield empty output depending on observed outcomes. Fallback is a best-effort policy, not an availability guarantee.

This assessment does not invent historical motivation or claim the deployed provider order matches defaults.

## Alternatives and when preferable

| Alternative | Trade-off | Prefer when |
| --- | --- | --- |
| One provider per capability | Simpler failure/contract/quota accounting; one outage or missing entitlement affects the capability | A provider offers sufficient coverage and the product accepts its dependency risk |
| Parallel provider racing | Can reduce tail latency but increases cost/quota use and needs deterministic result/cancellation rules | Measurements justify the spend and source equivalence is understood |
| Persisted market-data ingestion service | Removes provider fetch latency from many reads but introduces ingest scheduling, freshness/backfill and operations | Larger traffic/history workloads justify a durable ingestion capability |

## Review triggers

Revisit when quota/cost/latency measurements, data discrepancies, partial histories or provider licensing change requirements. Preserve result provenance and explicit-selection semantics in a prospective design change.
