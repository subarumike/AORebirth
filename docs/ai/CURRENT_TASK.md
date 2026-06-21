# Current Task

## Active Task

Fix login/re-entry seated-state persistence so characters who log out seated return seated.

## Current Scope

- Keep the fix scoped to persistence/load/spawn state handling.
- Do not reopen the already-fixed X-close seated logout timer unless the login issue shares the same state path.
- Preserve normal standing logout/login behavior.
- Preserve existing logout seated timer behavior.
- Do not touch unrelated gameplay code.
- Do not change database schemas or perform destructive database operations.

## Investigation Targets

- Character state serialization/save path during logout.
- Character load/deserialization path during login.
- Spawn/init code that may reset stance/action/movement state to standing.
- Packet/state broadcast sent to nearby clients and to the logging-in client after spawn.

## Validation Plan

- Run `cmd /d /c tools\build_aorebirth_debug.cmd`.
- Run `cmd /d /c restart-engines.cmd`.
- Run `git diff --check`.
- Review final `git status --short --branch`.
- Commit only intended source/doc changes.
