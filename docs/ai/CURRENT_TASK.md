# Current Task

## Current Focus

Make the Subway dungeon, resource/playfield `127`, fully playable using capture-backed implementation.

Subway work is the active development track. Playfield runtime decomposition is now maintenance work and should not be resumed unless Mike explicitly selects it.

## Current Implementation Order

1. Correct Subway NPC appearances.
2. Complete Subway entrance mob population.
3. Validate combat behavior.
4. Validate corpse/loot behavior.
5. Validate zoning into/out of Subway.
6. Complete additional Subway rooms from capture evidence.
7. Doors and scripted interactions.
8. Boss encounters.
9. Vendors and non-critical interactions.
10. Polish and parity against live captures.

## Current Subway State

- Subway content binding uses resource/playfield `127`.
- Runtime instance ids from live captures, such as `R=1187842`, are not server content binding ids.
- `Playfield2:122002` is capture/runtime output and must not be used as the Subway content binding key.
- Subway content work must stay capture-backed and should use completed AOSharpLiveCapture folders supplied by Mike.
- Mike launches AO client and capture tooling. Codex analyzes completed capture folders only.

## Completed Work

- Playfield runtime decomposition is completed through the latest extracted runtime services and is no longer the active focus.
- Corpse open, item loot transfer, and corpse credit payout are capture-backed and live validated.
- Subway entry/exit placement has been repaired for the current tested route.
- Filth Flea appearance has been corrected from capture-backed SCFU texture evidence.
- Subway content work is now the primary development track.

## Regression Risks Only

Preserve these while working on Subway:

- Playfield runtime service boundaries and lifecycle guardrails.
- Corpse open, loot transfer, corpse credit payout, and duplicate-loot prevention.
- NPC runtime, player combat runtime, and existing combat/corpse/despawn behavior.
- Existing private-city initialization, guest key, City Controller, and org behavior.
- Existing zoning, teleport, and visibility packet ordering.

## Validation Plan

For Subway content/code changes:

- `cmd /d /c git diff --check`
- focused Subway/content/SCFU tests when available
- `cmd /d /c tools\build_aorebirth_debug.cmd`
- `cmd /d /c restart-engines.cmd` when runtime behavior changes

Mike performs live AO client playtesting. Do not claim live validation unless Mike reports it.
