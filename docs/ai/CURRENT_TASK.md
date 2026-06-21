# Current Task

## Active Task

Add a root-level `restart-engines.cmd` wrapper for post-build AORebirth engine restarts.

## Current Scope

- Keep this workflow-only.
- Use cmd.exe-compatible scripts only.
- Do not redesign the engine startup system.
- Use the existing approved `stop-engines.cmd` and `start-engines.cmd` wrappers.
- Do not add slow polling loops, diagnostics, or unrelated checks.
- Do not touch gameplay code.
- Do not change database schemas or perform destructive database operations.

## Implementation

- Add `restart-engines.cmd` at the repository root.
- The wrapper should stop engines, start engines, report simple success/failure, and return through the wrapped commands' exit codes.
- Update workflow docs so future agents use `cmd /d /c restart-engines.cmd` after rebuilds.

## Validation Plan

- Run `cmd /d /c tools\build_aorebirth_debug.cmd`.
- Run `cmd /d /c restart-engines.cmd`.
- Run `git diff --check`.
- Review final `git status --short --branch`.
- Commit only intended source/doc changes.
