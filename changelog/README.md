# Changelog fragments

Pull requests never edit the root [`CHANGELOG.md`](../CHANGELOG.md) directly. Each
user-visible change adds one fragment file to `changelog/unreleased/`. Fragments accumulate
there until the developer asks an agent to compile them into `CHANGELOG.md`.
Fragments are separate files, so parallel pull requests no longer conflict on the changelog.

## Adding a fragment

Create `changelog/unreleased/<issue>-<slug>.md`, for example
`changelog/unreleased/969-add-savings-rate-dashboard-card.md`:

```markdown
---
type: Added
---
Add a savings rate dashboard card with monthly salary, spending and savings. #969
```

- `type` is one of `Added`, `Changed`, `Deprecated`, `Removed`, `Fixed`, `Security`.
- The body is exactly one line: the entry, written by the house rules in
  [`.claude/skills/changelog/SKILL.md`](../.claude/skills/changelog/SKILL.md). It ends with
  `#<issue>`, which must match the file name's issue number. A change without an issue uses a
  file name without the number (`fix-login-typo.md`) and omits the reference.
- File names are lower-case kebab-case and end in `.md`.

## Checking and compiling

Run from the repository root:

```bash
dotnet run scripts/CompileChangelog.cs -- --check
```

`--check` validates every fragment and runs in CI on every pull request and push.

Compiling is manual: when the developer asks for it, an agent runs
`dotnet run scripts/CompileChangelog.cs` on an up-to-date `develop` (or a branch cut from it).
The script folds the fragments into a `## [YY.M.D] - YYYY-MM-DD` block for today's UTC date
(merging into that block if it already exists) and deletes them. The agent reviews the new
dated block, then commits `CHANGELOG.md` together with the deleted fragments in one commit.
Do not compile as part of a feature change.
`dotnet run scripts/CompileChangelog.Tests.cs` runs the script's tests.
