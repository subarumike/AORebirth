# AI Changelog

## 2026-06-02 - Created AI Documentation System

Change: Added root AI handoff documentation.

Files affected:

- `AI_START_HERE.md`
- `docs/PROJECT_OVERVIEW.md`
- `docs/ARCHITECTURE.md`
- `docs/CURRENT_TASK.md`
- `docs/KNOWN_DECISIONS.md`
- `docs/ROADMAP.md`
- `docs/FEATURES.md`
- `docs/BUGS.md`
- `docs/TESTING.md`
- `docs/SESSION_NOTES.md`
- `docs/CODE_STANDARDS.md`
- `docs/CHANGELOG_AI.md`
- `docs/TODO.md`
- `docs/LESSONS_LEARNED.md`

Reason: Preserve project context across AI context compaction, session resets, model changes, and handoffs.

Follow-up work:

- Keep `docs/CURRENT_TASK.md` and `docs/SESSION_NOTES.md` current after each major work block.
- Add source-backed findings to `docs/KNOWN_DECISIONS.md` when behavior is locked.
- Move resolved bugs from `docs/BUGS.md` into changelog entries.

