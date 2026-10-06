# ADR 0005: Normalize portfolio movements without embedding valuation or return semantics

[Decision index](../09-architecture-decisions.md) · [Architecture index](../README.md)

- **Status:** Reconstructed / implemented, with an explicit partial-reuse boundary
- **Documented:** 2026-10-05
- **Original approval/date:** Not established by this record

## Context and observed decision

Portfolio money-weighted return and attribution both need consistent period movements from investment transactions and bond changes. Quote units, fees/rebates and zero-price quantity changes make duplicated normalization prone to divergence. The economic sign of a cash flow differs from its role in attribution; price/FX lookup is another concern.

[PortfolioPeriodLedger](../../../code/FinanceManager.Application/FinancialAccounts/Investments/Performance/PortfolioPeriodLedger.cs) normalizes movements and quantity changes only. It separates principal from fees/rebates, normalizes minor quote units, retains quantity for zero-price trades and maps bond unit changes through definitions. [MWR](../../../code/FinanceManager.Application/FinancialAccounts/Investments/Performance/PortfolioMoneyWeightedReturnService.cs) applies cash-flow signs and valuation/FX before XIRR; [attribution](../../../code/FinanceManager.Application/FinancialAccounts/Investments/Performance/PortfolioReturnAttributionService.cs) applies its decomposition/reconciliation semantics. [Ledger tests](../../../code/FinanceManager.Tests.Unit/Application/Services/Investments/PortfolioPeriodLedgerTests.cs) cover this normalization contract.

Current [TWR](../../../code/FinanceManager.Application/FinancialAccounts/Investments/Performance/PortfolioTimeWeightedReturnService.cs) still builds its own pending external-flow inputs. This record does **not** claim every performance calculation shares the ledger. Service comments identify buy/sell rows as the current external cash-impact representation established by issue #729, but that reference is not independently reconstructed as an original design decision here.

The observed decision is a **small application-level normalization seam**, with financial return/FX/valuation meaning kept in consumers.

## Consequences and present-day assessment

MWR and attribution can share transaction/bond normalization while retaining explicit consumer-specific signs and computations. The ledger has no provider calls and can be tested with fixed input fixtures.

TWR duplication remains a possible drift point, not automatic permission to refactor it. The current model is not a complete broker cash ledger, and buy/sell-as-external-impact semantics may differ from another portfolio model. Date boundaries, listing multipliers, missing bond definitions and fee currency meaning must remain explicit. A shared seam cannot itself guarantee accounting correctness or reproduce a production anomaly.

This is a present-day assessment from source, not invented historical rationale.

## Alternatives and when preferable

| Alternative | Trade-off | Prefer when |
| --- | --- | --- |
| Independent normalization per calculator | Simpler local edits but repeated quote/date/fee rules can diverge | Consumer models differ fundamentally and sharing would hide their financial meaning |
| One combined valuation/FX/return engine | Can coordinate/reuse input loading but risks a broad abstraction with coupled semantics | Proven repetition and agreed financial contracts justify a focused shared engine |
| Explicit cash subledger / event-based accounting | Models transfers, settlements and cash balances more richly but requires migration and new financial contracts | Product requirements need broker cash, settlement or accounting semantics beyond current buy/sell impact |

## Review triggers

Revisit when adding dividends/transfers/settlement/cash-account integration, changing fee currencies, or aligning TWR normalization. A financial change needs production-shaped or agreed domain fixtures before migration; do not infer correctness from names or a green unrelated test.
