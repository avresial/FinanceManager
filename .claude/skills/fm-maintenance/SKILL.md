---
name: fm-maintenance
description: Guide for AI agents using FinanceManager maintenance endpoints, including X-Maintenance-Key authentication, the price-backfill trigger, and bounded read-only log diagnostics.
---

# FMMaintenanceSkill

Use this skill for authorized maintenance automation and incident investigation. The maintenance
surface is deliberately narrow: it provides the existing price-backfill trigger and a read-only
log-history query. Never use the maintenance key as a substitute for user or administrator access.

## Authentication

Send the maintenance key only in this header:

```http
X-Maintenance-Key: <MAINTENANCE_KEY>
```

The application validates the key against its hashed database key or configured fallback. Keys are
never returned by maintenance endpoints, accepted in URLs, or written to logs. Use only
`<MAINTENANCE_KEY>` and `<BASE_URL>` placeholders in scripts and documentation.

## Endpoints

### Trigger backfill

`POST /api/PriceBackfill` queues the weekly closing-price backfill and returns `202 Accepted`.
It has no request body and is the only maintenance write-like operation. Do not call it while
investigating logs unless the operational task actually requires a backfill.

### Query logs

`GET /api/maintenance/logs` is strictly read-only. It returns the existing `PagedLogEntriesDto`:
`items` and `totalCount`. Each item contains `id`, `timestampUtc`, `level`, `category`, `message`,
`exception`, `eventId`, and `eventName` when available. Results are newest first.

Supported query parameters:

| Parameter | Default | Constraint |
|---|---:|---|
| `skip` | `0` | Non-negative number of matching entries to skip |
| `take` | `25` | Must be between `1` and `200` |
| `fromUtc` | none | Inclusive UTC timestamp lower bound |
| `toUtc` | none | Inclusive UTC timestamp upper bound |
| `level` | none | Exact `Trace`, `Debug`, `Information`, `Warning`, `Error`, or `Critical` level (case-insensitive) |
| `search` | none | Text matched against message, category, exception, or event name |

Keep the time window and `take` small first, then advance `skip` when `totalCount` exceeds the
page. Use ISO 8601 UTC values such as `2026-08-27T04:00:00Z`.

## Investigation workflow

1. Choose the incident window in UTC.
2. Request a small `Warning` or `Error` page for that window.
3. Add `search` terms for the provider, operation, or category.
4. Inspect `message`, `exception`, and event identity fields; redact sensitive values before sharing.
5. Page through additional results with `skip` only when needed.
6. Trigger `POST /api/PriceBackfill` separately only when remediation calls for it.

Example requests (placeholders are intentional):

```bash
curl -s -G "<BASE_URL>/api/maintenance/logs" \
  -H "X-Maintenance-Key: <MAINTENANCE_KEY>" \
  --data-urlencode "fromUtc=2026-08-27T04:00:00Z" \
  --data-urlencode "toUtc=2026-08-27T05:00:00Z" \
  --data-urlencode "level=Error" \
  --data-urlencode "take=25"
```

```bash
curl -s -X POST "<BASE_URL>/api/PriceBackfill" \
  -H "X-Maintenance-Key: <MAINTENANCE_KEY>"
```

## Responses and guardrails

- `200 OK`: log query succeeded.
- `202 Accepted`: backfill request was accepted.
- `400 Bad Request`: invalid level, time range, search length, or pagination bounds.
- `401 Unauthorized`: key is missing or invalid.
- `404 Not Found`: no maintenance key is configured; the route is intentionally masked.
- `429 Too Many Requests`: maintenance rate limit was exceeded; back off before retrying.

Only the documented GET query and backfill POST are available. Do not attempt log deletion,
mutation, configuration, admin, or unrelated write endpoints with this credential. Never place
the key in query parameters, command-line arguments that may be captured, logs, responses, or
committed files.
