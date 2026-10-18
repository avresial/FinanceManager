---
name: changelog
description: Use this skill whenever a code change introduces a user-visible difference (new feature, behaviour change, bug fix, removal, security patch). It explains how to add a changelog fragment under `changelog/unreleased/` (never editing the root `CHANGELOG.md` directly) so the project keeps a consistent, human-readable history any agent can extend.
---

# Changelog fragments

FinanceManager keeps a single `CHANGELOG.md` at the repository root in [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) format. It is the source of truth — GitHub renders it, the in-app changelog view (issue #185 / follow-up) will fetch it, and LinkedIn/release posts are written from it.

**Never edit `CHANGELOG.md` directly.** Each change adds one fragment file under `changelog/unreleased/`; fragments accumulate there until the developer asks an agent to compile them into `CHANGELOG.md`. Separate files keep parallel pull requests from conflicting on the changelog. Entries are still curated by hand — one fragment per user-visible change.

## Versioning model

The project uses **CalVer in the form `YY.M.D`** (year.month.day, no leading zeros), stamped automatically by `Directory.Build.props` (see issue #183). A compiled block is named after the UTC date of the compile; there is no `[Unreleased]` section.

## Compiling fragments (only when the developer asks)

Compiling is a manual step. When the developer asks you to compile (or "compact") the changelog:

1. Start from an up-to-date `develop` (`git checkout develop && git pull origin develop`), or a branch cut from it.
2. Run `dotnet run scripts/CompileChangelog.cs` from the repository root. It folds all pending fragments into `## [YY.M.D] - YYYY-MM-DD` for today's UTC date (merging into that block if it already exists, skipping duplicates) and deletes the compiled fragments. It refuses to change anything while a fragment is invalid.
3. Review the new dated block in `CHANGELOG.md` against the house rules below.
4. Commit `CHANGELOG.md` together with the deleted fragments in one commit.

Never compile as part of a feature or bugfix change.

## Adding a fragment

Create `changelog/unreleased/<issue>-<slug>.md`, where `<issue>-<slug>` is normally the branch name (see `CLAUDE.md` → "Branch naming"):

```markdown
---
type: Fixed
---
Show the correct currency on bond transaction rows. #174
```

- **Front matter** holds exactly one key, `type`, set to one of the sections below (case-sensitive).
- **Body** is exactly one non-empty line: the entry, following the house rules. A leading `- ` is optional; the compiler adds the list marker.
- **File name** is lower-case kebab-case ending in `.md`. If it starts with an issue number, the entry must end with that `#<issue>`. A change without an issue uses a name without a number (`tidy-login-copy.md`) and omits the reference.
- One fragment per entry. A change that needs two entries (e.g. an addition and a separate fix) adds two fragments: `<issue>-<slug>.md` and `<issue>-<other-slug>.md`.

Validate before committing (from the repository root); the `changelog-check` CI job runs the same check on every pull request:

```bash
dotnet run scripts/CompileChangelog.cs -- --check
```

## Allowed sections

Use only these six types. In the compiled block they appear in this fixed order, omitting empty ones:

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
3. **Skip non-user-visible changes.** Pure refactors, test-only changes, CI tweaks, dependency bumps without behavioural impact, and internal docs do **not** get a fragment. If in doubt: would a user, beta tester, or future-you reading release notes care? If no, skip.
4. **Group by user value, not by implementation.** A single feature spread across many commits/PRs gets one entry. Multiple unrelated fixes get one entry each.
5. **No code identifiers** in entries unless they're user-facing (page names, settings, labels). Don't mention class names, method names, namespaces, file paths.
6. **Markdown only**, no HTML. Inline links are fine.

## Workflow checklist for an agent making a change

When you finish a code change that has a user-visible effect:

1. Add `changelog/unreleased/<issue>-<slug>.md` with the `type` front matter and a one-line entry ending with `#<issue>`.
2. Run `dotnet run scripts/CompileChangelog.cs -- --check` and fix any reported error.
3. Stage the fragment together with the code change in the same commit. The commit message already references the issue per project convention — the entry mirrors it.
4. To reword a pending entry, edit its fragment. Dated `[YY.M.D]` blocks in `CHANGELOG.md` are history; do not change them.

## What NOT to do

- Don't edit `CHANGELOG.md` in a feature branch, and don't add an `[Unreleased]` section — `--check` rejects it.
- Don't run the compile (without `--check`) unless the developer asked for it.
- Don't auto-generate entries from `git log` — entries are curated, not dumped.
- Don't add extra front-matter keys, tables of contents, or "Contributors" sections.
- Don't put credentials, internal URLs, or customer data into entries.
