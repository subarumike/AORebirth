# Current Task

## Active Task

Investigate and begin implementing the missing terminal-use system for Statel-backed terminal interactions.

## Current Scope

- Work only in AORebirth.
- Keep changes scoped to terminal/statel use handling and capture-backed implementation.
- Use live capture evidence before implementing terminal behavior.
- Do not fabricate terminal event semantics.
- Do not touch AO client stripdown, AO rebuild, or unrelated projects.
- Do not add unrelated cleanup or rebranding work.
- Do not change database schemas or perform destructive database operations.

## Investigation Targets

- Generic command handler for Action Use (3).
- Statel lookup and event dispatch.
- Terminal-specific Statel event model.
- Existing capture tools/docs for live client behavior.
- Packets/events emitted when affected terminal `Terminal 0000C73D:C00204A2` is used.
- Whether the 2 Statel events represent selection, requirements, dialog, teleport, shop, mission, or another terminal subsystem.

## Validation Plan

- Use the repo-approved AOSharp live capture workflow when available.
- Document the captured packet/event sequence, or the blocker if capture cannot be collected.
- Run `cmd /d /c tools\build_aorebirth_debug.cmd`.
- After successful rebuild, run `cmd /d /c restart-engines.cmd`.
- Validate the affected terminal against the captured scenario.
- Run `cmd /d /c git diff --check`.
- Review final `cmd /d /c git status --short --branch`.
- Commit and push only the scoped fix and required task/docs updates.
