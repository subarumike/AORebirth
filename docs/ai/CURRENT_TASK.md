# Current Task

## Current status

- `docs/project/PROJECT_STATE.md` is now the primary Codex memory file.
- AI startup/read order now reads `docs/project/PROJECT_STATE.md` before this file.
- Rex works through B18F on the current stable baseline.
- Marcus quest chain is not implemented.
- The Marcus dirty vertical slice was rolled back.
- Worktree has intentional docs changes only, plus a pre-existing `.gitignore` modification.
- Next development task is not selected yet.

## Stable baseline

- Current stable baseline commit: `0946690`.

## Active task

No active implementation task selected. Await user instruction.

## Explicit non-goals

- Do not rebuild Marcus chain yet.
- Do not modify Rex unless user asks.
- Do not alter gate behavior.
- Do not stage or commit without user instruction.

## Next safe options

- Review `docs/project/PROJECT_STATE.md` before selecting any new implementation task.
- Select the next development target explicitly before code changes.
- Audit Marcus evidence before any Marcus work.
- If continuing Rex, inspect current Rex state and generated result history before touching code.
