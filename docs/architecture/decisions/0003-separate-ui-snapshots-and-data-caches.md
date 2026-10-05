# ADR 0003: Separate rendered UI snapshots from request-skipping data caches

[Decision index](../09-architecture-decisions.md) · [Architecture index](../README.md)

- **Status:** Reconstructed / implemented
- **Documented:** 2026-10-05
- **Original approval/date:** Not established by this record

## Context and observed decision

Dashboard cards and account histories benefit from immediately showing their last rendered content when revisited. Financial data can change independently, so a persisted rendered state must not implicitly suppress the fresh request.

The [snapshot contract](../concepts/ui-snapshots.md) and [SnapshotRefreshCoordinator](../../../code/FinanceManager.Components/Shared/Services/SnapshotRefreshCoordinator.cs) explicitly separate snapshots from TTL data caching. A snapshot is read and painted, then the fresh request always runs. Rendered-content equality suppresses equal rendering/storage updates; failure preserves painted content. The caller owns context and refresh version gates. Storage is best effort and diagnostic messages avoid owner-bearing keys.

This is distinct from `LocalStorageStateCacheService`, which may skip fetching within a validity window, and from server repository/dashboard caches. [CacheInvalidator](../../../code/FinanceManager.Application/Dashboard/CacheInvalidator.cs) invalidates user-scoped server tags after writes. [Coordinator tests](../../../code/FinanceManager.Tests.Unit/Components/Shared/Services/SnapshotRefreshCoordinatorTests.cs) represent the behavioral contract.

## Consequences and present-day assessment

The user can regain useful context without waiting for a network round trip while freshness is still requested. Recoverable fetch/storage failures preserve continuity. Stable user/account/currency/date/filter keys and race guards are essential; transient selections/paging/expansion remain surface state rather than shared snapshot truth.

Snapshots contain financial display data in browser storage and require careful identity/context separation. They do not establish authorization or a verified current value. Server caches require write invalidation, and a default HybridCache registration does not establish distributed invalidation. More persisted surface types mean schema/version/size concerns; use the guide's contracts rather than adding unrelated caching behavior to each component.

These benefits and obligations follow the implementation; original motivation beyond the explicit snapshot guide is not inferred.

## Alternatives and when preferable

| Alternative | Trade-off | Prefer when |
| --- | --- | --- |
| Fetch-only rendering | Less persistence/context machinery; revisits wait and failures can leave an empty surface | A surface is cheap, rarely revisited, or sensitive enough to prohibit persistence |
| TTL response cache only | Fewer requests; may deliberately reuse data without checking freshness | The feature explicitly accepts a freshness window and wants request suppression |
| Shared browser query-state library | Centralized policy/invalidation, but adds dependency/migration costs and can blur rendered versus source data | Existing mechanisms cannot serve broader coordinated query requirements without excessive duplication |

## Review triggers

Revisit when persistence/privacy requirements change, storage limits cause real failures, surfaces leak context or multi-instance invalidation becomes necessary. Preserve the paint-then-always-fetch behavior unless an explicitly approved freshness contract changes it.
