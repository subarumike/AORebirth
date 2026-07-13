# Current Task

## Current Focus

Live-validate the global visibility-interest runtime, then perform a bounded rollout of the 38 quarantined PF127 Subway rows from capture `20260710-202132`.

## Remaining Step

1. Confirm the current safe 221-row population through login/relog, traversal across visibility boundaries, player and NPC movement, combat/death, corpse appearance/re-entry/loot/despawn, respawn, zoning, and unchanged static/vendor visibility.
2. Run the existing controlled sequence `NONE`, `SUPPORTED_29`, then `ORDINARY_9`, recording the spatial and packet transport diagnostics for each session.
3. Keep every failing slice quarantined. Activate the full 259-row population only after Mike reports repeatable client success for both bounded slices and the combined population.

## Constraints

- Do not claim live validation from builds or automated tests.
- Do not unquarantine any row before the controlled client rollout succeeds.
- Do not change packet fields, enemy profiles/spawns, RoomSpace handling, or add pacing, batching, throttling, or pagination during this validation.
- Keep the ordinary-enemy runtime and the client-side RoomSpace guard separate from visibility-interest diagnosis.
- Use `docs/project/VISIBILITY_INTEREST.md` as the implemented architecture reference.

## Completion Evidence

The task is complete only when Mike confirms stable repeated login and traversal with all 259 captured rows active and the recorded diagnostics show bounded selection with complete SCFU, weapon-definition, CharInPlay, corpse, and Despawn delivery.
