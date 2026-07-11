# Current Task

## Current Focus

Make the Subway dungeon, resource/playfield `127`, fully playable from capture-backed evidence.

## Implementation Roadmap

### Phase 1 - NPC Population And Combat

- Complete the Subway NPC population.
- Correct NPC appearances from captured SCFU evidence.
- Correct patrol paths from captured movement evidence.
- Validate combat behavior.

### Phase 2 - Static World And Interactions

- Complete static world objects.
- Add doors.
- Add containers.
- Add interactive objects.
- Add capture-backed environmental details.

### Phase 3 - Dungeon Progression

- Implement room-by-room parity with live captures.
- Add named NPCs.
- Add scripted events.
- Add boss encounters.
- Complete dungeon progression.

### Phase 4 - Supporting Content And Polish

- Add vendors.
- Add quest interactions.
- Complete remaining polish.
- Validate parity against live captures.

## Active Development Rules

- Prefer visible gameplay improvements over architectural refactoring.
- Use live AO captures as the authoritative implementation source.
- Implement Subway incrementally within the single resource/playfield `127`.
- Do not resume Playfield decomposition unless Mike explicitly requests it.
- Avoid speculative fixes; implement behavior only from capture-backed evidence.

## Resolved Def-Agg Finding

The repeated chat hint:

`Use the Def-Agg slider in the Stats view to change between defensive and aggressive.`

is live-confirmed level 1 client behavior, not an AORebirth combat regression.

- Live AO level 1 characters show this chat hint on every enemy attack.
- This is not Subway-specific.
- This is not Thief-specific.
- This also happens with enemies such as Malfunctioning Cleaning Robot.
- Do not make more Def-Agg combat changes.
- Keep prior combat packet changes only when independently supported by capture evidence.
- Disable temporary combat diagnostics before normal playtesting so logs do not fill with `COMBAT_START_DIAG`.

## Current Subway State

- Subway content binding uses resource/playfield `127`.
- Runtime instance ids from live captures, such as `R=1187842`, are not server content binding ids.
- `Playfield2:122002` is capture/runtime output and must not be used as the Subway content binding key.
- Subway content work must stay capture-backed and should use completed AOSharpLiveCapture folders supplied by Mike.
- Mike launches AO client and capture tooling. Codex analyzes completed capture folders only.

## Completed Work

- Playfield runtime decomposition is completed through the latest extracted runtime services and is maintenance work only unless Mike explicitly selects it.
- Corpse open, item loot transfer, and corpse credit payout are capture-backed and live validated.
- Previous capture-backed enemy, movement, combat, zoning, and appearance work remains completed project history.
- Subway entry/exit placement has been repaired for the current tested route.
- Filth Flea appearance has been corrected from capture-backed SCFU texture evidence.
- Captures `20260709-205921`, `20260709-210452`, `20260709-212115`, and `20260709-212336` now provide a reusable raw-packet and continuous-survey evidence set for the explored Subway sections.
- The current supported-type population baseline contains 95 capture-backed Filth Flea, Discarded Pet, Disobedient Bot, Mugger, Thief, and Violent Vagabond spawns, replacing the earlier 32-spawn subset without stacking duplicate populations. Captures `20260709-220439` and `20260709-222339` added eighteen spatially distinct deeper supported-type positions; later same-position respawn identities were not added as duplicate spawns.
- Packet-backed Mugger and Violent Vagabond visuals are applied from the recovered SCFU texture/mesh profiles.
- The ordinary capture-backed framework now adds 126 deduplicated PF127 spawns for Shadow, Stim Fiend, Workman/Architect Striker, Infected Attendant, Slum Runner, Looter, Infector, Lost Thought, and Neural Burnout. It constructs attackable characters without guessed database templates and preserves captured SCFU identity/appearance/unknown fields, stats, movement paths, combat evidence, and observed loot. `Healer`, boss-owned Infectors, and all named/boss archetypes are excluded.
- Guard telemetry now proves the Subway client crash is not bad AORebirth spawn data: separate Workman Striker and Infector queries preserve captured actor X/Z while the client derives invalid heights (`Y-0.75`, `Y-1.50`, or `Y=0`) and receives room-cell result `-1`. Server actors and captured paths remain unchanged; the guarded client converts that legitimate no-cell result to `nullptr` instead of allowing the original client path to throw.
- `Tools/AOClientRoomSpaceGuard/ProxyDll` provides the completed RoomSpace-only `version.dll` repair for normal AO shortcuts. Exact install/uninstall validation passed, the verified package is installed in both supported clients (`C:\Funcom\Anarchy Online` for local testing and `D:\Funcom\Anarchy Online` for live testing), and pre/post hashes prove both client EXEs and `N3.dll` files remain unchanged. Mike confirmed the guarded clients no longer crash. Treat this repair as closed unless a new regression appears; active work has returned to capture-backed PF127 Subway content using completed capture `20260709-222339`, with named/boss/archetype behavior implemented only through bounded evidence-backed contracts.
- Mike reports `20260709-222339` completes the remaining live Subway traversal. The capture includes Abmouth Supremus, Eumenides, Vergil Aeneid, Bitaxel, Bloodcreeper, Empty Shell, Fragmented Soul, Incomplete Rebuild, Melded Patterns, Molested Molecules, Premature Pattern, and Redundant Scan evidence; these remain evidence-only until their bounded boss/archetype contracts are implemented.
- Def-Agg level 1 combat hint has been closed as live-confirmed behavior, not a regression.
- Generic unarmed NPC attacks without captured or equipped weapon context no longer emit `AttackInfo`. This removes the client-generated `nanobots / unknown damage` combat text while preserving health changes and capture-backed/equipped attack packets.
- First-room Thief behavior is restored as an isolated capture-backed slice: the existing Thief spawn/visual now has captured patrol replay from `20260710-205400`, creates a corpse, has a 60-second respawn after despawn, and drops QL1 Stolen Handbag (`297055/297055`) from corpse loot. The handbag mission chain remains deferred.

## Regression Risks Only

Preserve these while continuing Subway room-by-room work:

- Playfield runtime service boundaries and lifecycle guardrails.
- Corpse open, loot transfer, corpse credit payout, and duplicate-loot prevention.
- NPC runtime, player combat runtime, and existing combat/corpse/despawn behavior.
- Existing private-city initialization, guest key, City Controller, and org behavior.
- Existing zoning, teleport, and visibility packet ordering.

## Validation Plan

For Subway content changes:

- `cmd /d /c git diff --check`
- focused Subway/content tests when code changes
- `cmd /d /c tools\build_aorebirth_debug.cmd` when code changes
- `cmd /d /c restart-engines.cmd` when runtime behavior changes

Mike performs live AO client playtesting. Do not claim live validation unless Mike reports it.
