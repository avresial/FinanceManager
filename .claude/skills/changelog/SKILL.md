---
name: changelog
description: Use this skill whenever a code change introduces a user-visible difference (new feature, behaviour change, bug fix, removal, security patch). It explains how to add or update entries in the root `CHANGELOG.md` so the project keeps a consistent, human-readable history any agent can extend.
---

# Maintaining `CHANGELOG.md`

FinanceManager keeps a single `CHANGELOG.md` at the repository root. It follows the [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) format. This is the source of truth — GitHub renders it, the in-app changelog view (issue #185 / follow-up) will fetch it, and LinkedIn/release posts are written from it. There is no auto-generation: every change that ships goes in by hand.

## Versioning model

The project uses **CalVer in the form `YY.M.D`** (year.month.day, no leading zeros), stamped automatically by `Directory.Build.props` (see issue #183). A new version block is opened **every time a change lands on `develop`** that we'd want users to know about — there is no separate "release" step. Today's date determines the version number; if multiple changes land the same day, group them under the same `YY.M.D` block.

- Top of file always has an `## [Unreleased]` section. Work-in-progress entries accumulate there.
- When a change is merged to `develop`, rename `[Unreleased]` to `[YY.M.D]` with today's date and add a fresh empty `[Unreleased]` above it. If a `[YY.M.D]` block already exists for today, merge the new entries into it instead of creating a duplicate.
- Dates are ISO 8601 (`YYYY-MM-DD`).

## File structure

```markdown
# Changelog

All notable changes to FinanceManager are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
CalVer (`YY.M.D`).

## [Unreleased]

### Added
- Short, user-focused description of a new feature. #<issue>

## [26.5.25] - 2026-05-25

### Added
- ...

### Changed
- ...

### Fixed
- ...
```

Entries are in **reverse chronological order** (newest at the top, just below `[Unreleased]`).

## Allowed sections

Use only these six headings, in this fixed order, omitting any that are empty:

| Section | Use for |
|---|---|
| `Added` | New features or capabilities visible to a user. |
| `Changed` | Behaviour/UX changes to existing functionality. |
| `Deprecated` | Features still available but slated for removal. |
| `Removed` | Features that no longer exist. |
| `Fixed` | Bug fixes. |
| `Security` | Vulnerability patches and hardening. |

## Writing entries — house rules

1. **One line per entry.** A short sentence in present tense, user-perspective. *Good:* "Add date-range filter to account transaction lists." *Bad:* "Refactored `TransactionFilterController.GetByDate` to accept nullable `DateTime`."
2. **Reference the issue.** Every entry ends with the GitHub issue number it resolves, prefixed with `#` — e.g. `... #174`. If a change has no issue, omit the reference rather than invent one. This matches the project's commit message rule (`CLAUDE.md` → "Commit Messages").
3. **Skip non-user-visible changes.** Pure refactors, test-only changes, CI tweaks, dependency bumps without behavioural impact, and internal docs do **not** belong in the changelog. If in doubt: would a user, beta tester, or future-you reading release notes care? If no, skip.
4. **Group by user value, not by implementation.** A single feature spread across many commits/PRs gets one line. Multiple unrelated fixes get one line each.
5. **No code identifiers** in entries unless they're user-facing (page names, settings, labels). Don't mention class names, method names, namespaces, file paths.
6. **Markdown only**, no HTML. Inline links are fine.

## Workflow checklist for an agent making a change

When you finish a code change that has a user-visible effect:

1. Open `CHANGELOG.md`.
2. Locate or create the `## [Unreleased]` block at the top.
3. Under the appropriate section heading (`### Added` / `### Changed` / etc., creating the heading if missing), append a one-line entry following the rules above, ending with `#<issue>`.
4. Stage `CHANGELOG.md` together with the code change in the same commit. The commit message already references the issue per project convention — the changelog line mirrors it.
5. Do not write to past `[YY.M.D]` blocks; history is immutable once dated.

## Promoting `[Unreleased]` to a release

This is normally done as part of the merge that ships the change:

1. Rename `## [Unreleased]` to `## [YY.M.D] - YYYY-MM-DD` using today's UTC date (matching `Directory.Build.props`).
2. Insert a new empty `## [Unreleased]` block above it.
3. If a block for today's date already exists, fold the entries into it (preserve section order, no duplicates) and delete the redundant header.

## What NOT to do

- Don't auto-generate from `git log` — entries are curated, not dumped.
- Don't add front-matter, tables of contents, or "Contributors" sections.
- Don't reorder existing dated blocks; only the top `[Unreleased]` block is mutable.
- Don't edit `CHANGELOG.md` in a commit that has no other code/doc changes unless the user explicitly asks for a changelog-only fix.
- Don't put credentials, internal URLs, or customer data into entries.
