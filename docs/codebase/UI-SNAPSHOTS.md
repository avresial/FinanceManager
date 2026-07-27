# UI Snapshots (stale-while-revalidate) vs. time-based data caching

The client has two mechanisms that both persist data in browser local storage. They look similar
and are not interchangeable. Pick by asking one question: **may the API request be skipped?**

| | **UI snapshot (stale-while-revalidate)** | **Time-based data cache** |
|---|---|---|
| Type | `ISnapshotRefreshCoordinator` over `ISnapshotService` | `LocalStorageStateCacheService<TState, TRefreshContext, TCacheKey>` |
| API request | **Always** runs | **Skipped** while the cached entry is still valid |
| What is stored | The last *rendered* state of a UI surface | The last *fetched* data, with its validity window |
| Why | The surface paints instantly instead of flashing a spinner | Fewer requests for data that does not change often |
| Freshness | Guaranteed on every visit — reconciled against the response | Bounded by the entry's expiry rules |
| Use for | Dashboard cards, transaction lists, charts | Nav menu state, assets-page cards, investment rate/estimate lookups |

A UI snapshot is never a source of truth. It is what the user looks at during the few hundred
milliseconds the real request takes.

## The workflow

`SnapshotRefreshCoordinator.RunAsync` performs, in order:

1. Read the snapshot stored under the request's key.
2. Map it to a rendered model and hand it to `OnSnapshotPainted` — the surface renders immediately.
   When there is no usable snapshot, `OnSnapshotMissing` runs instead (show a loading state there).
3. Run the fresh request — always, snapshot or not.
4. Compare the fresh model with the painted one using the request's `ContentComparer`.
5. Only when they differ: hand the fresh model to `OnRefreshed` and overwrite the stored snapshot.

Guarantees the coordinator makes so no caller has to re-implement them:

- **Equal data is inert.** A refresh that matches what is on screen repaints nothing and writes nothing.
- **Metadata never counts as a change.** Equality runs over the *rendered model*, so `SnapshotBase.FetchedAtUtc`
  and anything else that only lives on the snapshot cannot make an unchanged surface look changed.
- **A failed refresh is not destructive.** With a snapshot on screen the stale content stays; only a
  surface with nothing to show reports a blocking failure (`SnapshotRefreshResult.IsBlockingFailure`).
- **Storage failures are non-fatal.** An unreadable snapshot is logged, evicted, and the run continues
  as a plain load. A failed write is logged and never discards the fresh result.
- **Stale runs cannot win.** With a `RefreshVersionGate`, a run superseded by a newer one commits
  neither UI nor storage.

## Using it

```csharp
// One gate per refreshable surface, on the component that owns the state.
private readonly RefreshVersionGate _gate = new();

var result = await SnapshotRefreshCoordinator.RunAsync(new SnapshotRefreshRequest<MySnapshot, MyModel>
{
    Key = $"my-surface:{userId}:{currencyId}",
    Gate = _gate,
    ToModel = snapshot => snapshot.ToModel(),
    FetchAsync = () => MyHttpClient.Get(userId, currencyId, start, end),
    ToSnapshot = MySnapshot.FromModel,
    OnSnapshotPainted = model => Render(model),
    OnSnapshotMissing = () => ShowSpinner(),
    OnRefreshed = model => Render(model),
});

if (result.IsBlockingFailure)
    ShowError();
```

### Keys

Scope the key to the owning user and to whatever else changes *what is rendered* — currency,
account id. Do **not** put the selected date range in the key: one surface keeps one snapshot that
each save overwrites, rather than accumulating an entry per range the user has ever picked. Store
the range inside the snapshot instead when the view needs to know what produced it (the dashboard
does this so amounts and their period label stay paired during a reload).

### Content equality

The default `JsonContentComparer<TModel>` treats two models as equal when they serialize
identically. Keep the model free of fields that move on every fetch — a request timestamp, a
server-generated id — or the surface repaints and rewrites storage every time. When such a field
cannot be avoided, pass a custom `IEqualityComparer<TModel>` as `ContentComparer`.

### Empty results vs. no result

`FetchAsync` returning `null` means *no usable response came back*. It is treated as a soft failure:
a painted snapshot stays on screen and storage is left alone. A surface that legitimately has **no
data** must return an *empty model* instead — an empty model compares unequal to a populated
snapshot, so it clears the UI and replaces the stored snapshot, which is what "the account now has
zero entries" should do. Returning `null` for that case would silently keep stale content visible.

### Preserving state the snapshot cannot carry

A snapshot stores rendered content only. When the API response carries more than that — pagination
cursors, neighbouring-entry markers such as `NextOlderEntry` — apply the *fetched object itself* on
refresh and use the model purely for equality and persistence. Rebuilding the response from the
model would silently drop those fields; the account-details pages keep the fetched account in a
local and hand it to their apply step for exactly this reason.

### Component structure

Data fetching and snapshot reconciliation belong in the page coordinator or a container component,
not in a reusable view component. View components should take a prepared model and render it. The
dashboard follows this shape: `Dashboard.razor.cs` resolves user and currency, runs the coordinator,
and hands finished card models down to the cards.

## Where it is used

| Surface | Entry point |
|---|---|
| Dashboard overview | `code\FinanceManager.Components\Features\Dashboard\Components\Dashboard.razor.cs` |
| Account transaction lists | `code\FinanceManager.Components\Features\FinancialAccounts\Services\AccountDetailsSnapshotStore.cs` |

`AccountDetailsSnapshotStore` shows the recommended shape for a surface with several callers: a thin
feature-level wrapper that owns the key shape and the snapshot↔model mapping, leaving the workflow
to the coordinator. Follow-up card and chart migrations should add a similar wrapper rather than
re-implementing the orchestration.
