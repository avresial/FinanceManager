# AGENTS.md

Instructions for AI agents working on FinanceManager.

**Read CLAUDE.md first** - CLAUDE.md contains the information usualy found in agents md.


- **Project conventions** (build, tests, architecture, branching, changelog): read [`CLAUDE.md`](./CLAUDE.md).
- **Using the running app** — above all the **develop-only auto test login** (`/DevelopLogin/{login}/{page}`)
  that signs you in as `guest` or `testuser` without the landing page or login form: read
  [`.claude/skills/finance-manager-usage/SKILL.md`](./.claude/skills/finance-manager-usage/SKILL.md).
  Never walk the landing page → login form → "Check out demo" sequence when testing.
- **Rendering/screenshotting UI changes** in the cloud sandbox: read
  [`.claude/skills/ui-testing/SKILL.md`](./.claude/skills/ui-testing/SKILL.md).

## Issue lifecycle after opening a PR

After creating or updating a pull request, revisit every linked issue and update its lifecycle label to match the current state. Keep `in progress` while an agent is actively implementing, validating, or remediating the change. Once the implementation is complete, validated, pushed, and no agent is actively working, replace `in progress` with `ready for merge`. After the PR is merged, close the issue when the merged change fully satisfies it.
