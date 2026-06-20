# Current Task

## Current status

- `docs/project/PROJECT_STATE.md` is now the primary Codex memory file.
- AI startup/read order now reads `docs/project/PROJECT_STATE.md` before this file.
- Rex works through B18F on the current stable baseline.
- Marcus quest chain is not implemented.
- The Marcus dirty vertical slice was rolled back.
- Last live-smoked committed quest baseline is `ecbca7d` (`Implement Marcus B18F to B194 transition`).
- Uncommitted Marcus Phase 4B item `296780` handout work exists in `MarcusB18FCompletionHandler.cs`.
- The item handout has focused ZoneEngine build/search validation, but it has not had live smoke.
- Marcus quest work is paused and on the back burner.
- Worktree also has a pre-existing `.gitignore` modification.
- Next development task should be selected from non-quest gameplay bugs.

## Stable baseline

- Current stable baseline commit: `0946690`.

## Active task

Marcus quest work paused. Await user selection of a non-quest gameplay bug.

## Explicit non-goals

- Do not continue Marcus quest implementation.
- Do not run Marcus live smoke unless Mike explicitly resumes it.
- Do not implement gas fire use.
- Do not implement B196 or B197.
- Do not implement Flint.
- Do not implement KnuBot trade.
- Do not add Marcus rewards.
- Do not add DB mission persistence.
- Do not modify Rex unless user asks.
- Do not alter gate behavior.
- Do not stage or commit without user instruction.

## Next safe options

- Review `docs/project/PROJECT_STATE.md` before selecting any new implementation task.
- Select the next development target explicitly before code changes.
- Prefer non-quest gameplay bugs for the next work item.
- Before switching work, decide whether to commit the validated-but-unsmoked item handout or revert it.
