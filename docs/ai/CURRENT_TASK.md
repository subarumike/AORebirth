# Current Task

## Active Task

Capture and implement the surgery clinic implant-install flow after terminal activation.

## Current Scope

- Work only in AORebirth.
- Keep changes scoped to surgery-clinic state, implant install validation, inventory/equipment mutation, and required packets.
- Use live capture evidence before implementing post-terminal implant behavior.
- Do not rework the already-implemented surgery-clinic terminal-use route unless capture proves it blocks implant install.
- Do not guess implant behavior.
- Do not touch AO client stripdown, AO rebuild, or unrelated projects.
- Do not add unrelated cleanup or rebranding work.
- Do not change database schemas or perform destructive database operations.

## Investigation Targets

- What client packet/action is sent when installing an implant after surgery clinic use.
- Whether surgery clinic activation creates a timed character state, nano/effect flag, skill-lock, treatment modifier window, or server-side permission window.
- Implant install validation for source inventory slot, target wear slot, treatment requirement, ability/stat requirements, and any profession/breed/side checks.
- Inventory-to-equipment mutation path for implants.
- Outbound packets required after a successful implant install, including inventory, equipment, stats, feedback, and any action availability updates.
- Failure behavior when the clinic is not active or requirements are not met.

## Validation Plan

- Use the repo-approved AOSharp live capture workflow when available.
- Capture surgery-clinic use followed by a successful implant install, and failure behavior without clinic if practical.
- Document the captured packet/event sequence under `docs/generated/`, or the blocker if capture cannot be collected.
- Run `cmd /d /c tools\build_aorebirth_debug.cmd`.
- After successful rebuild, run `cmd /d /c restart-engines.cmd`.
- Validate surgery-clinic activation, implant install, source inventory update, equipment/wear update, and persistence if applicable.
- Run `cmd /d /c git diff --check`.
- Review final `cmd /d /c git status --short --branch`.
- Commit and push only the scoped fix and required task/docs updates.
