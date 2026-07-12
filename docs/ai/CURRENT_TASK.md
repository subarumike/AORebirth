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
- First-room Thief behavior is restored as an isolated capture-backed slice: the existing Thief spawn/visual now has captured patrol replay from `20260710-205400`, a 60-second respawn after despawn, and QL1 Stolen Handbag (`297055/297055`) corpse loot. Raw packet `CorpseFullUpdate #1580` from that capture proves the exact 412-byte corpse shape, CATMesh `5907`, MonsterData `26092`, and material tail; the server now uses that exact template instead of the crashing MonsterData-as-CATMesh fallback. Mike live-validated the interim suppressed-visual build did not crash on kill; the restored exact corpse visual and handbag interaction remain to be live-validated. The deferred mission chain is unchanged.
- AOSharpLiveCapture now promotes generic mob lifecycle evidence into `npc-lifecycle.csv` and decodes every raw `CorpseFullUpdate` into `corpse-full-updates.csv`, with completeness checks in capture validation. `decode_npc_lifecycle_capture.py` retro-decodes existing capture folders, so raw evidence already collected does not require another gameplay run.
- Captured enemy combat is now an atomic runtime contract instead of a per-mob toggle. Supported and generated ordinary Subway spawns register one of four explicit attack models (`FixedAttackInfo`, `EquippedWeapon`, `Specialized`, or `Unresolved`); the shared registry drives retaliation and attack-source selection, cleans up on despawn, validates required fields, equips captured weapon templates, and logs incomplete evidence instead of silently producing harmless enemies. Existing captures provide ready contracts for Filth Flea, Discarded Pet, Mugger, Thief, Violent Vagabond, and all ordinary archetypes with observed `AttackInfo`; Disobedient Bot and Lost Thought remain explicitly unresolved because retaliation was observed without a landed hit/source.
- Live capture `20260711-170337` proves the bounded Thief retaliation contract: QL1 Solar-Powered Pistol `121567`, `SpecialAttackWeapon` header `Unknown=0` with body values `32/32/32/32/0`, `Attack` header `Unknown=0`, attack start `1.409765s` after the echoed player attack, movement transition `0.219999s` after the Thief attack, first landed hit `11.409643s` after the Thief attack (`12.819408s` after the player echo), fixed normal damage `9`, `AmmoCount=-1`, slot `6`, `Unk1=0`, weapon instance `0`, and approximately six-second repeats. Its captured movement transition is emitted as Target -> `StopMovingCmd` -> `SetPos` -> `NpcPath`, and its captured `StopFight` precedes Death. Private capture `20260711-172309` supplied the pre-repair comparison: header `Unknown=1`, body `32/2/2/2/0`, reordered/duplicate movement, and no dying-NPC `StopFight`.
- The implementation now keeps those values behind explicit Thief contract fields and separate attack-start/movement/first-hit deadlines. Other equipped NPCs retain the legacy item damage bonus, timing, `AmmoCount=40`, and `Unk1=4`; only capture-ready/known combat paths maintain movement during recharge. Focused combat, packet-envelope, death, corpse, and loot tests pass `9/9`, and the approved AORebirth build passes. A post-repair live capture is still required before claiming live parity.
- The centralized damage-calculation system now has a side-effect-free core boundary in `ZoneEngine.Core.DamageCalculator`, with deterministic random input, request/result/trace models, evidence classifications, fixed captured damage representation, and legacy normal-hit preservation through `CombatDamageRules`. The ordinary weapon-input provenance audit selected Outcome B: partial input provenance. `WeaponDamageRequestBuilder` can now produce diagnostic-only request construction results, provenance records, and missing/malformed input issues, but it is not wired into active production damage. Repository fields expose weapon min/max, legacy damage bonus, raw damage type, timing, attack-skill dictionaries, some AMS caps, and damage-type AC/add-damage mappings, but local evidence still does not prove critical bonus source, critical-state resolution, Add All Off ordering, AMS-cap zero semantics, armor formula/order, or universal add damage. Production damage therefore remains legacy/fixed, and Subway Thief remains fixed `9`.
- The ordinary weapon-hit parity follow-up is evidence-only and keeps production damage unchanged. `WeaponDamageObservationValidator`, `WeaponDamageCandidateEvaluator`, `WeaponDamageParityReporter`, and the opt-in `WeaponDamageDiagnosticSnapshotBuilder` now support schema-backed observation validation, report-only candidate comparison, synthetic evaluator controls, and an initial underdetermined parity report. No live ordinary observations were fabricated. The Subway Thief record remains a fixed captured `9` behavior only, not ordinary formula proof.
- First controlled ordinary weapon-hit post-fix session `starter-pistol-postfix-001` validated corrected AORebirth legacy behavior for QL1 Solar-Powered Pistol `121567` against Arete `Malfunctioning Cleaning Robot` targets: 13 valid ordinary-hit rows, 0 incomplete, 0 rejected, emitted damage values `9, 18, 6, 18, 5, 17, 15, 8, 7, 8, 11, 2, 11`, observed range `2-18`, active `legacyDamageBonus=0`, valid weapon range bypassing the player fallback floor, and no duplicate/overlapping damage. The evidence tool now accepts lethal overkill rows when health delta equals `min(observedDamage, targetHealthBefore)`. No original AO formula is activated, and production damage remains corrected legacy/fixed behavior.
- The heartbeat health-state audit classified the pending `InvalidOperationException` swallow as `MASKS_UPSTREAM_DEFECT`. `ZoneEngineLog.txt` proves the observed failure was a duplicate health stat (`Sequence contains more than one matching element`) during the NPC attacker scan; normal construction supplies exactly one positive-default health and life stat, but the runtime producer of the duplicate remains unresolved. The swallow was removed. Only positive current health below maximum health can pass NPC regeneration, zero/negative current health is treated as dead, zero/negative maximum health and current health at/above maximum are skipped without mutation, and only characters targeting the NPC have health read during the attacker scan. Missing or duplicate health on a relevant attacker remains observable as upstream corruption. Player regeneration, combat timing, death/corpse ordering, and regeneration values are unchanged.

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

## Latest Subway Combat Checkpoint Validation

- `cmd /d /c tools\run_aotomation_messaging_tests.cmd /Tests:CombatStartPacketsUseLiveCompatibleBaseFlagAndDoNotEmitDefAggTutorialText,NpcCombatAttackRulesPreserveCapturedCleaningRobotContextDecision,SubwayThiefCombatContractPreservesLiveEnvelopeMovementAndDeathOrder,SubwayFilthFleaCombatUsesCapturedPoisonAndMeleeAttackContext,CleaningRobotDeathOrderIncludesStopFightDeathCorpseAndDespawnScheduling,CleaningRobotNpcAttackOrderKeepsSpecialAttackWeaponBeforeAttackInfo,NpcCorpseLifecycleRulesPreserveCapturedCleaningRobotDeathTimings,CorpseLootCreditGuardrailPreservesAccessTransferAndCreditOwnership,CapturedCombatAndInventoryPacketsUseStandardN3Envelope`: PASS, `9/9`.
- `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS.
- `cmd /d /c tools\run_aotomation_messaging_tests.cmd /TestCaseFilter:"FullyQualifiedName~SmokeLounge.AOtomation.Messaging.Tests.PlayfieldLifecycleTraceTests"`: `39/44` pass. The five failures are older source/data guardrails outside this Thief slice.
- `cmd /d /c tools\run_aotomation_messaging_tests.cmd`: `128/134` pass. The same five lifecycle failures plus the pre-existing inventory ownership guardrail fail; all six are baseline guardrail failures unrelated to the new Thief expectations.

## Latest Heartbeat Checkpoint Validation

- `cmd /d /c tools\run_aotomation_messaging_tests.cmd /Tests:PlayfieldCharacterHeartbeatHealthRulesCoverRuntimeModelStates,PlayfieldCharacterHeartbeatStatsContractSurfacesMissingOrDuplicateHealth`: PASS, `2/2`.
- Focused heartbeat, combat, death, corpse, and loot selection: PASS, `7/7`.
- Previous focused combat/lifecycle selection: PASS, `9/9`.
- `cmd /d /c tools\run_aotomation_messaging_tests.cmd /TestCaseFilter:FullyQualifiedName~PlayfieldLifecycleTraceTests`: `39/44` pass; the same five baseline lifecycle guardrails fail.
- `cmd /d /c tools\run_aotomation_messaging_tests.cmd`: `130/136` pass, improving only by the two new heartbeat tests; the same six pre-existing guardrail failures remain unchanged from the `128/134` pre-edit baseline.
- `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS. The approved engine restart completed with ChatEngine, LoginEngine, and ZoneEngine listening on their expected ports.
