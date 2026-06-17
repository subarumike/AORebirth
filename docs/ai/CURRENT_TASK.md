# Current Task

Generated: 2026-06-16

## Current Objective

Make NPCs face the player when a dialogue interaction opens.

Scope:

- Add a narrow dialogue-facing helper on `NPCController`.
- Apply it to legacy KnuBot dialogue starts.
- Apply it to the gated Rex Larsson Arete dialogue route before the chat window opens.
- Use existing recovered `SetWantedDirection` packet support only for dialogue-facing; do not alter normal NPC chase movement.
- No schema changes, quest behavior, rewards, inventory, XP/credits, mission state execution, quest packets, packet semantics guesses, validation infrastructure, or report/export tooling.

## Active Questions

- Manual smoke result: PASS. The local client visibly shows the NPC turning toward the player when dialogue opens.

## Validation Plan

- Build focused ZoneEngine.
- Restart engines with `AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING=1`.
- Run Rex aggregate validation/dry-run.
- Run Arete validation harness.
- Run `git diff --check`.
- Manual in-client smoke result: PASS.
