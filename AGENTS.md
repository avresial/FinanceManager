# AGENTS.md

Instructions for AI agents working on FinanceManager.

- **Project conventions** (build, tests, architecture, branching, changelog): read [`CLAUDE.md`](./CLAUDE.md).
- **Using the running app** — above all the **develop-only auto test login** (`/DevelopLogin/{login}/{page}`)
  that signs you in as `guest` or `testuser` without the landing page or login form: read
  [`.claude/skills/finance-manager-usage/SKILL.md`](./.claude/skills/finance-manager-usage/SKILL.md).
  Never walk the landing page → login form → "Check out demo" sequence when testing.
- **Rendering/screenshotting UI changes** in the cloud sandbox: read
  [`.claude/skills/ui-testing/SKILL.md`](./.claude/skills/ui-testing/SKILL.md).
