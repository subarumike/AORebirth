# Current Task

## Current Focus

Fix the global combat regression where the AO client repeatedly prints:

`Use the Def-Agg slider in the Stats view to change between defensive and aggressive.`

This is now the active priority and overrides Subway dungeon content work.

## Current Regression Scope

- This is not Subway-specific.
- This is not Thief-specific.
- This happens with multiple enemies, including Malfunctioning Cleaning Robot.
- Do not change Subway content while fixing this regression.
- Use completed captures/evidence only for packet comparison.
- Do not make speculative one-field packet fixes.
- Find the exact raw packet or player/combat state trigger before code changes.

## Failed Or Incomplete Fixes

These changes did not resolve the repeated Def-Agg tutorial message by themselves:

- `AttackMessage.Unknown=0`
- `AttackInfoMessage.Unknown=0`
- delayed non-robot first combat tick
- login/actionable `State=0`

Review each prior fix only for independent live compatibility. Keep or revert based on evidence, not on whether it fixed this symptom.

## Next Required Action

Perform a raw packet-level comparison of global player-vs-NPC combat start between AORebirth and official live captures.

Prioritize repeated combat-start emissions:

1. `SpecialAttackWeapon`
2. player `Attack` echo
3. `AttackInfo`
4. `NumFightingOpponents`
5. combat stance/action state
6. player state/stat updates
7. unknown packets emitted at attack start

If the raw packet evidence is still insufficient, improve capture/tool logging first. Do not commit another guessed combat-field change.

## Paused Subway Track

Subway dungeon work is paused until the global Def-Agg combat regression is fixed.

Paused implementation order:

1. Correct remaining Subway NPC appearances.
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

- Playfield runtime decomposition is completed through the latest extracted runtime services and is maintenance work only unless Mike explicitly selects it.
- Corpse open, item loot transfer, and corpse credit payout are capture-backed and live validated.
- Subway entry/exit placement has been repaired for the current tested route.
- Filth Flea appearance has been corrected from capture-backed SCFU texture evidence.

## Regression Risks Only

Preserve these while fixing the global Def-Agg combat regression:

- Playfield runtime service boundaries and lifecycle guardrails.
- Corpse open, loot transfer, corpse credit payout, and duplicate-loot prevention.
- NPC runtime, player combat runtime, and existing combat/corpse/despawn behavior.
- Existing private-city initialization, guest key, City Controller, and org behavior.
- Existing zoning, teleport, and visibility packet ordering.

## Validation Plan

For Def-Agg regression investigation:

- `cmd /d /c git diff --check`
- focused combat packet/player-state tests when code changes
- `cmd /d /c tools\build_aorebirth_debug.cmd` when code changes
- `cmd /d /c restart-engines.cmd` when runtime behavior changes

Mike performs live AO client playtesting. Do not claim live validation unless Mike reports it.
