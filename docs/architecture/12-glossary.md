# 12. Glossary

[Architecture index](README.md)

Terms below describe FinanceManager's current model. Refer to [financial semantics](08-crosscutting-concepts.md#81-domain-and-financial-semantics) before changing calculations; the names alone are not a substitute for sign, date, rounding and currency rules.

## 12.1 Domain and technical vocabulary

| Term | Meaning in this repository |
| --- | --- |
| Account / financial account | A user's tracked container of financial data; currency, bond and investment accounts have different entry/transaction and valuation rules |
| Account entry | A dated currency or bond account record, including a value/value change; accumulated balances may need recalculation after earlier mutations |
| Posting date | Effective date of an account entry; used for ordering, balance history and bond holding changes |
| Trade date | Date of an investment transaction; distinct from the time a price provider publishes a quote |
| Asset | Instrument identity shared across listings; not synonymous with a user's holding |
| Asset listing | A tradable listing of an asset with exchange/quote-currency/multiplier context; pricing resolves at this level |
| Market-data symbol | Provider-specific identifier and configuration used to fetch a listing's market data |
| Price quote | A stored market observation for a listing and time/currency; an as-of valuation can use the latest available prior observation |
| Price multiplier | Scale converting raw quote units to major currency units; e.g. a GBX listing can require `0.01` to yield GBP |
| GBX / minor quote unit | Pence-style quote denomination; must be normalized before major-currency FX conversion |
| Holding / position | Quantity held for a listing/account as of a date, derived from transactions and opening history |
| Valuation | Monetary value of holdings at an as-of date, with price-unit normalization and conversion into the requested currency |
| Opening-boundary history | Entries/positions preceding the selected window required to establish opening holdings/balances; excluding them can distort series and returns |
| Portfolio | User-scoped investment and bond holdings considered together by performance services; not an independently authorized tenant |
| Portfolio period ledger | Application-level normalized movements/quantity changes for a selected period; separates principal, fees/rebates and bond movements without applying valuation, FX or return semantics |
| Contribution / withdrawal | External cash-impact representation in current portfolio calculations; buys/sells and bond unit changes supply these effects in the current model |
| Fee / fee rebate | Charge or returned charge associated with a transaction; represented separately in ledger/attribution, with consumer-specific cash-flow signs |
| Money-weighted return (MWR) | Annualized return sensitive to timing and size of dated cash flows and opening/ending portfolio values; calculated with XIRR |
| XIRR | Irregular-date internal-rate-of-return calculation over signed cash flows; failure/insufficient inputs are reported with status |
| Time-weighted return (TWR) | Return assembled from valuation periods with external flows, reducing the effect of contribution/withdrawal timing under the implemented convention |
| Return attribution | Decomposition of portfolio value change into non-overlapping components; residual market/valuation effects must not be represented as independently measured causal effects |
| Unrealized gain/loss | Difference between current holding valuation and the applicable cost basis; distinct from cash flow and annualized return |
| FX / exchange rate | Conversion ratio between currencies for a date; exact, derived, carried and unpublished/missing values have different provenance/status |
| Carry-forward rate | Prior available value used for a later date under resolution policy; not proof an exact rate was published on the later date |
| Not yet published | Provider/result status for data expected later, potentially carrying a safe retry time; distinct from a permanent missing value |
| Net worth / liabilities | Asset and obligation measures in the requested currency; different services calculate point and time-series views |
| Money flow | Inflow/outflow/net-cash-flow and related budgeting/label analysis; distinct from valuation and portfolio performance |
| Label / transaction rule | Classification attached to financial records / configured conditions and actions used to classify imports or transactions |
| Bond details | Instrument definition, including unit value/currency and calculation parameters required to value bond entries |
| Capitalization | Applying accrued bond interest to the calculation base according to the model's defined method and timing |
| Rendered snapshot | Locally persisted content of the last rendered card/list; painted immediately while a fresh request always runs |
| Data cache / TTL | Stored request/derived result reused during a time-to-live; may skip a request, unlike the rendered snapshot contract |
| Refresh version gate | Surface-owned ordering guard ensuring an older async response cannot replace the newer user/context refresh |
| Cache invalidation tag | Owner/context grouping used to remove derived server cache entries after a mutation; `dash:u…` and `global:u…` are user-scoped tags |
| Guest sandbox | Optional isolated, temporary guest data/session store; process-local lifecycle differs from regular relational-user persistence |
| Typed HTTP client | Browser-side service encapsulating API route/request/response details; the UI's transport boundary to the server |
| Layered modular monolith | Feature modules organized within dependency layers and released through one API host; not a set of independently deployed services |
| Vertical slice | Related feature concerns grouped within each layer's feature folders without violating the inward dependency rules |
| ADR | Architecture decision record stating context, decision, consequences, evidence and alternatives; reconstructed records do not prove historical approval |
| MCP | Model Context Protocol endpoint/tools for authorized clients to access FinanceManager data |
| SPA JWT | Browser application bearer token with issuer/audience/signature/lifetime validation; separate from an MCP OAuth grant |
| OAuth grant / reference token | Persisted MCP authorization/token state validated by OpenIddict, with independent revocation and scope/resource rules |
| PKCE | Proof Key for Code Exchange protecting configured OAuth authorization-code clients; redirect URIs remain exact registered values |
| Liveness / readiness | Process responsiveness / ability to serve traffic including dependencies; `/alive` and `/health` have distinct semantics |
| OTLP / OpenTelemetry | Optional telemetry export protocol / instrumentation used for traces, metrics and logs; collection without an exporter does not establish retained observability |
| PWA | Progressive Web App shell/install/service-worker capabilities; not a promise of offline financial editing |
| RTO / RPO | Recovery-time / recovery-point objective. No agreed numerical targets are claimed by this baseline |

Domain contracts/entities live under [FinanceManager.Domain](../../code/FinanceManager.Domain), orchestration under [FinanceManager.Application](../../code/FinanceManager.Application), and external/persistence implementations under [FinanceManager.Infrastructure](../../code/FinanceManager.Infrastructure). The [integration guide](concepts/integrations.md) defines provider-specific names and configuration.
