# Current Task

## Active Task

Prevent bags/backpacks/container items from being placed inside other bags/backpacks/containers.

## Current Scope

- Keep the change in the existing inventory/container move validation path.
- Do not rewrite inventory, bank, or backpack systems.
- Do not change database schemas or perform destructive database operations.
- Preserve normal non-container item moves into backpacks.
- Preserve backpack-to-inventory moves.
- Add focused logging/validation consistent with existing patterns.

## Validation Plan

- Run `cmd /d /c tools\build_aorebirth_debug.cmd`.
- Run `git diff --check`.
- Review final `git status --short --branch`.
- Commit only intended source/doc changes for this task.
