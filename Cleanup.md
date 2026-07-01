# Backend Cleanup Audit

Audit of the backend layers (`FinanceManager.Api`, `FinanceManager.Application`,
`FinanceManager.Domain`, `FinanceManager.Infrastructure`). Migrations and generated
files are excluded.

## Overall assessment

The backend is in good shape. Clean-architecture boundaries are respected:

- `FinanceManager.Domain` has **no** EF Core / ASP.NET references (verified — the only
  `AppDbContext` / `EntityFrameworkCore` mentions in `Application` are inside code comments,
  not real dependencies).
- No business logic / DB access leaks into controllers of note; controllers stay thin.
- Naming conventions are consistent: private fields are `_camelCase`, interfaces `IPascalCase`,
  namespaces match folders. No violations found by scanning private/public field declarations.

The findings below are therefore mostly about **complexity / size** and one **latent bug**,
not systemic architecture problems.

## Findings & tasks

### 1. `BondEntryRepository.AddLabel` is a no-op (latent bug) — DONE
`Infrastructure/Repositories/Account/Entry/BondEntryRepository.cs:445` fetches the entry and
the label, then calls `SaveChangesAsync()` **without ever adding the label to the entry**. It
returns `true` while doing nothing. `CurrencyEntryRepository.AddLabel` implements the same
contract correctly. `AddLabel` currently has no production call sites (only the bulk `AddLabels`
path is used), so the bug is latent — but it should be fixed and covered by a test.
- **Action**: mirror the correct `CurrencyEntryRepository` implementation; add a unit test.

### 2. `Api/Program.cs` (339 lines) — too much inline configuration — DONE
The startup file inlines options binding for ~12 option types, the full JWT/CORS/forwarded-headers
setup, and a long block of hosted-service registrations. This is readable but monolithic.
- **Action**: extract cohesive `IServiceCollection` extension methods
  (`AddApiOptions`, `AddApiSecurity`, `AddApiBackgroundServices`) so `Program.cs` reads as a
  high-level outline. Pure move — no behaviour change.

### 3. `Application/.../Stock/Resolution/InstrumentResolver.cs` (400 lines) — mixed responsibilities (recommended follow-up)
The resolver does three jobs: (a) orchestrate the AV + OpenFIGI fan-out and reconcile candidates,
(b) **persist** the auto-resolved instrument into the asset model (`PersistInvestmentModel`,
`BuildIdentifiers`, `ResolveVenue`, `MapAssetType`, `PriceMultiplierFor`), and (c) manage the
OpenFIGI failure cooldown. The persistence + mapping half is a distinct responsibility.
- **Action**: extract a `ResolvedInstrumentPersister` collaborator (with the venue/type/identifier
  mapping helpers) injected into the resolver. Left as a follow-up because it sits on the pricing
  hot path and changes the manually-wired constructor in `Infrastructure/ServiceCollectionExtension.cs`;
  it warrants its own focused PR even though `InstrumentResolverTests` already exists.

### 4. `BondEntryRepository` (567 lines) — duplicated neighbour-lookup logic (recommended follow-up)
The class carries two implementations of the per-instrument "next older / next younger" lookup:
the explicit-interface `IBondAccountEntryRepository.GetNextOlder/GetNextYounger(accountId, date)`
run an **N+1 query per BondDetailsId in a loop**, while `GetNextOlderPerInstrument` /
`GetNextYoungerPerInstrument` already express the same intent as a single set-based query.
- **Action**: collapse the looping explicit-interface implementations onto the set-based query to
  remove the N+1 and the duplication. Left as a follow-up (needs repository test coverage first).

### 5. Large infrastructure files worth watching (no action now)
`CurrencyEntryRepository.cs` (484), `AccountRepository.cs` (337),
`CachedAccountEntryRepository.cs` (336), `Api/Services/CurrencyImportJobStore.cs` (277),
`AlphaVantageClient.cs` (220). These are cohesive; noted for future monitoring, not split now.
