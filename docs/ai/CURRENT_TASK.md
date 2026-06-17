# Current Task

Generated: 2026-06-17

## Current Objective

Add the first gated live objective-progress path for Rex Larsson mission B18C.

Scope:

- Preserve existing gated Rex content-driven dialogue behavior.
- Preserve the safe DTO-based B18C `QuestFullUpdate` sender.
- Keep `AO_REBIRTH_ENABLE_ARETE_REX_DIALOGUE_ROUTING`, `AO_REBIRTH_ENABLE_ARETE_REX_QUEST_PREVIEW`, and the new `AO_REBIRTH_ENABLE_ARETE_REX_B18C_PROGRESS` disabled by default.
- Track only `Mission:5514B18C` objective progress in memory.
- Count only live kills of `Malfunctioning Cleaning Robot` for the player who received the gated B18C preview.
- Do not emit quest progress packets unless packet fields are evidence-backed.
- Do not implement completion, rewards, inventory, XP/credits, DB writes, quest deletion, Cargo Box behavior, B18D/B18E behavior, broad Arete quest routing, or action `59` interpretation.

## Current Status

- Rex dialogue works.
- The safe B18C `QuestFullUpdate` sender works in live smoke.
- `Mission:5514B18C` appears in the client mission window.
- B18C in-memory live progress tracking is implemented behind `AO_REBIRTH_ENABLE_ARETE_REX_B18C_PROGRESS`.
- Focused ZoneEngine build passed.
- Rex inactive content dry-run passed.
- Arete validation harness passed 131 cases.
- `git diff --check` passed with line-ending warnings only.
- Default engines were restarted with all three Rex gates cleared.

## Active Questions

- Capture `20260614-194454` contains named `Malfunctioning Cleaning Robot` spawn/full-update/death/corpse evidence, including level, HP, `monsterData=297023`, and coordinates.
- Read-only local DB verification found no runtime `Malfunctioning Cleaning Robot` spawns in playfield `6553`; live `5/5` smoke is blocked until the captured robot observations are promoted into evidence-backed runtime spawn data or an explicitly approved isolated test path.
- Does the client mission window need a later evidence-backed progress refresh packet, or is server-side in-memory progress sufficient for this phase?

## Validation Plan

- Run focused ZoneEngine build.
- Run Rex inactive content dry-run.
- Run Arete validation harness if Arete framework/content is touched.
- Run `git diff --check`.
- When evidence-backed robot spawns exist, start engines with all three Rex gates enabled and run a manual in-client smoke:
  - `/tp 3624.599 787.7465 51.745 6553`
  - talk to Rex
  - start B18C so it appears in the mission window
  - kill `Malfunctioning Cleaning Robot` if available
  - observe ZoneEngine logs for progress
- Restore all three gates off after smoke.
