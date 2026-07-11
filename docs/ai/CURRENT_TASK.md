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
- The current supported-type population baseline contains 124 capture-backed Filth Flea, Discarded Pet, Disobedient Bot, Mugger, Thief, and Violent Vagabond spawns, replacing the earlier 32-spawn subset without stacking cross-capture duplicate populations. Capture `20260710-202132` adds 29 exact-SCFU-canonical missing positions and preserves the two separately observed Violent Vagabonds that stood only `0.721m` apart. The same capture corrects Disobedient Bot to live `NpcFamily=138` and `CharacterFlags=268964353`.
- Packet-backed Mugger and Violent Vagabond visuals are applied from the recovered SCFU texture/mesh profiles.
- The ordinary capture-backed framework now adds 135 deduplicated PF127 spawns for Shadow, Stim Fiend, Workman/Architect Striker, Infected Attendant, Slum Runner, Looter, Infector, Lost Thought, Neural Burnout, and Deranged Shopper. Capture `20260710-202132` contributes two new Looter anchors, six Stim Fiend anchors, and one Deranged Shopper with exact raw-SCFU textures, meshes, flags, unknown bytes, waypoints, and observed `9`-point combat damage; Deranged Shopper loot remains empty because no corpse was captured. The framework constructs attackable characters without guessed database templates and preserves captured evidence. `Healer`, boss-owned Infectors, and named/boss archetypes remain excluded.
- Guard telemetry now proves the Subway client crash is not bad AORebirth spawn data: separate Workman Striker and Infector queries preserve captured actor X/Z while the client derives invalid heights (`Y-0.75`, `Y-1.50`, or `Y=0`) and receives room-cell result `-1`. Server actors and captured paths remain unchanged; the guarded client converts that legitimate no-cell result to `nullptr` instead of allowing the original client path to throw.
- `Tools/AOClientRoomSpaceGuard/ProxyDll` remains the normal-shortcut `version.dll` repair lane, but the first four-callsite proxy build regressed the new C client before world entry: Windows WER recorded `anarchyonline.exe` heap-corruption crash `0xc0000374` in `ntdll.dll` with `C:\Funcom\Anarchy Online\VERSION.dll` loaded, while AORebirth logs showed only Login connect/disconnect and no Zone login. The proxy package is now narrowed to the one observed crashing RoomSpace callsite per supported client (`new N3+0x16144`, `old N3+0x148B6`) to preserve the proven Subway repair while avoiding the three unobserved early collision callsites. Package build/self-tests pass and the narrowed proxy is installed in both the C and D client folders.
- Mike reports `20260709-222339` completes the remaining live Subway traversal. Follow-up capture `20260710-202132` completed cleanly with 102 tracked entities and closes the reported missing Violent Vagabond, Disobedient Bot, Mugger, Discarded Pet, Looter, Deranged Shopper, and Stim Fiend population slice through 38 new spawn anchors. Focused Subway tests, the Debug build, and Chat/Login/Zone restart pass; Mike's private-server room traversal remains the required live validation. Named/boss evidence from `20260709-222339` remains evidence-only until bounded contracts are implemented.
- Completed Thief-only capture `20260710-205400` supplies the existing PF127 Thief with its repeated eight-leg walk cycle, a capture-matched `60`-second respawn after the dead NPC identity despawns (about `70.1` seconds after the observed death), and exact SCFU appearance value `1576`.
- Completed handbag-mission capture `20260710-212455` adds Natalia Akcora at her exact PF655 position and SCFU appearance, the captured offer/accept/turn-in dialogue, mission `Mission:554D28C7`, cross-zone mission updates, mission-active QL1 Stolen Handbag loot (`297055/297055`), inventory-backed trade consumption, Daily Mission XP Reward item action (`285612`), and captured action-59 plus quest-delete completion packets. The active mission player may be present while a helper lands the final Thief hit, so drop eligibility is playfield-wide rather than final-attacker-only. Mission state is process-local. Hotfix `2026-07-10`: captured handbag QuestFullUpdate/quest-delete/completion UI packet emission is disabled by `EmitCapturedQuestUiPackets=false` after private-server login crashed while the same client logged into live; dialogue, active-session loot gating, trade consumption, and reward action remain wired. Private acceptance, helper-kill loot, zone round-trip, turn-in, reward, relog smoke, and safe QuestFullUpdate parity remain required.
- A capture folder path supplied by Mike means that capture is already stopped and complete. Analyze it immediately; do not ask Mike to stop it or add post-capture collection steps.
- AOSharpLiveCapture now treats one-off quest lines as comprehensive single-pass evidence. It records all decoded N3 fields/arrays in `decoded-messages.jsonl`, all readable player stats at capture boundaries and every five seconds in `player-stat-snapshots.csv`, and produces `quest-capture-coverage.json` so acceptance, objectives, dialogue, zoning, combat attribution, loot, trade, completion, rewards, and stat deltas can be recovered without recreating the character.
- PF127 login regression `2026-07-10 22:17:05` was a server heartbeat failure, not a client content crash: template-free captured NPC nano regeneration evaluated derived `StatMaxNanoEnergy` without the profession/breed inputs it requires. Heartbeat regeneration now skips nano regeneration only when that derived stat is unavailable, advances the interval, and leaves health regeneration, combat, patrols, and player regeneration unchanged. Debug build, test-assembly build, and Chat/Login/Zone restart pass; fresh PF127 login smoke remains required.
- Def-Agg level 1 combat hint has been closed as live-confirmed behavior, not a regression.

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
