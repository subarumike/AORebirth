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
- Do not mark a Subway enemy accepted until `AcceptedSubwayEnemyGateRequiresWholeEnemyCoverage` covers spawn, movement/chase, combat contract, weapon context, corpse visual, loot, respawn, and loot/despawn behavior together.

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
- Loot-bearing corpse close/reopen now follows official-live capture `20260712-195019` for the same `(Corpse:F6C002)`: open sends `InventoryUpdate`; close sends `Action 0x66`, `CharacterAction 110`, and the normal Use acknowledgement without an inventory refresh; reopen sends a new `InventoryUpdate` with a refreshed handle. Capture 51 proves handles `113 -> 114 -> 115 -> 116` across successive reopens and one remaining item after the captured loot transfer. The rejected refresh-plus-`0x66` path is removed. Manual client validation remains required.
- Loot-bearing corpses retain their five-minute lifetime while loot remains; empty or fully looted corpses use a one-second cleanup delay, matching the near-immediate live disappearance observed after the final transfer.
- Previous capture-backed enemy, movement, combat, zoning, and appearance work remains completed project history.
- Subway entry/exit placement has been repaired for the current tested route.
- Filth Flea appearance has been corrected from capture-backed SCFU texture evidence.
- Mike live-validated the next hallway Filth Flea on `2026-07-12` as working as intended for the tested gameplay path. Filth Flea also has captured corpse loot evidence in completed Subway captures `20260709-210452` and `20260709-220439`; item rows are represented in the supported Subway loot table, and observed nonzero corpse credits `29..79` are represented in `CombatCorpseRules`. Keep it as a passed hallway smoke result, but do not promote Filth Flea into `AcceptedSubwayEnemyGateRequiresWholeEnemyCoverage` until respawn/no-respawn expectations are explicitly covered by the accepted-enemy gate.
- Captures `20260709-205921`, `20260709-210452`, `20260709-212115`, and `20260709-212336` now provide a reusable raw-packet and continuous-survey evidence set for the explored Subway sections.
- The supported-family provider retains 124 capture-backed Filth Flea, Discarded Pet, Disobedient Bot, Mugger, Thief, and Violent Vagabond evidence rows. The 29 rows sourced from capture `20260710-202132` are runtime-quarantined because enabling that restored batch reproduced a PF127 login crash during the existing-character visibility snapshot. The prior 95-row supported-family runtime baseline remains active while the failing slice is isolated.
- Packet-backed Mugger and Violent Vagabond visuals are applied from the recovered SCFU texture/mesh profiles.
- The ordinary capture-backed framework retains 135 deduplicated PF127 evidence rows, but the nine rows sourced from capture `20260710-202132` are runtime-quarantined with the supported-family batch after the client repeatedly crashed during PF127 login visibility delivery. The prior 126-row ordinary runtime baseline remains active. Deranged Shopper and the added Looter/Stim Fiend rows remain checked in for bounded reintroduction; named bosses, `Healer`, owned summons, unsupported families, and duplicates remain excluded.
- Guard telemetry now proves the Subway client crash is not bad AORebirth spawn data: separate Workman Striker and Infector queries preserve captured actor X/Z while the client derives invalid heights (`Y-0.75`, `Y-1.50`, or `Y=0`) and receives room-cell result `-1`. Server actors and captured paths remain unchanged; the guarded client converts that legitimate no-cell result to `nullptr` instead of allowing the original client path to throw.
- `Tools/AOClientRoomSpaceGuard/ProxyDll` provides the completed RoomSpace-only `version.dll` repair for normal AO shortcuts. Exact install/uninstall validation passed, the verified package is installed in both supported clients (`C:\Funcom\Anarchy Online` for local testing and `D:\Funcom\Anarchy Online` for live testing), and pre/post hashes prove both client EXEs and `N3.dll` files remain unchanged. Mike confirmed the guarded clients no longer crash. Treat this repair as closed unless a new regression appears; active work has returned to capture-backed PF127 Subway content using completed capture `20260709-222339`, with named/boss/archetype behavior implemented only through bounded evidence-backed contracts.
- Mike reports `20260709-222339` completes the remaining live Subway traversal. Follow-up capture `20260710-202132` completed cleanly and supplies a fully classified 38-row candidate population slice, but enabling the entire slice reproduced the previous PF127 login crash. All 38 candidate rows remain preserved in code and the manifest while a runtime quarantine restores the last working population. Reintroduce them only in bounded slices with a successful login check after each slice.
- Def-Agg level 1 combat hint has been closed as live-confirmed behavior, not a regression.
- Generic unarmed NPC attacks without captured or equipped weapon context no longer emit `AttackInfo`. This removes the client-generated `nanobots / unknown damage` combat text while preserving health changes and capture-backed/equipped attack packets.
- First-room Thief behavior is restored and Mike live-validated it against the current client on `2026-07-12`: combat behavior, projectile attack text, pistol-based damage rolls, captured movement/attack context, exact corpse visual, guaranteed QL1 Stolen Handbag (`297055/297055`) corpse loot, loot-bearing corpse persistence past the empty cleanup window, empty-corpse cleanup, and 60-second respawn now match live behavior for this slice. Raw packet `CorpseFullUpdate #1580` from capture `20260710-205400` proves the exact 412-byte corpse shape, CATMesh `5907`, MonsterData `26092`, and material tail. The deferred mission chain is unchanged.
- AOSharpLiveCapture now promotes generic mob lifecycle evidence into `npc-lifecycle.csv`, decodes every raw `CorpseFullUpdate` into `corpse-full-updates.csv`, records state-event provenance in `enemy-state.csv`, and writes `enemy-respawns.csv` for death-to-same-archetype/same-position respawn timing. Respawn captures marked with `/aocap mark respawn-start` validate incomplete unless a respawn is correlated. `decode_npc_lifecycle_capture.py` retro-decodes existing capture folders, so raw evidence already collected does not require another gameplay run.
- Captured enemy combat is now an atomic runtime contract instead of a per-mob toggle. Supported and generated ordinary Subway spawns register one of four explicit attack models (`FixedAttackInfo`, `EquippedWeapon`, `Specialized`, or `Unresolved`); the shared registry drives retaliation and attack-source selection, cleans up on despawn, validates required fields, equips captured weapon templates, and logs incomplete evidence instead of silently producing harmless enemies. Existing captures provide ready contracts for Filth Flea, Discarded Pet, Mugger, Thief, Violent Vagabond, and all ordinary archetypes with observed `AttackInfo`; Disobedient Bot and Lost Thought remain explicitly unresolved because retaliation was observed without a landed hit/source.
- Live capture `20260711-170337` proves the bounded Thief retaliation contract: QL1 Solar-Powered Pistol `121567`, `SpecialAttackWeapon` header `Unknown=0` with body values `32/32/32/32/0`, `Attack` header `Unknown=0`, attack start `1.409765s` after the echoed player attack, movement transition `0.219999s` after the Thief attack, first landed hit `11.409643s` after the Thief attack (`12.819408s` after the player echo), fixed normal damage `9`, `AmmoCount=-1`, slot `6`, `Unk1=0`, weapon instance `0`, and approximately six-second repeats. Its captured movement transition is emitted as Target -> `StopMovingCmd` -> `SetPos` -> `NpcPath`, and its captured `StopFight` precedes Death. Private capture `20260711-172309` supplied the pre-repair comparison: header `Unknown=1`, body `32/2/2/2/0`, reordered/duplicate movement, and no dying-NPC `StopFight`.
- The implementation now keeps those values behind explicit Thief contract fields and separate attack-start/movement/first-hit deadlines. Other equipped NPCs retain the legacy item damage bonus, timing, `AmmoCount=40`, and `Unk1=4`; only capture-ready/known combat paths maintain movement during recharge. Focused combat, packet-envelope, death, corpse, and loot tests pass, the approved AORebirth build passes, and Mike's current-client live playtest confirms Thief parity for the accepted slice.
- Mike's `2026-07-12` live checks proved the Thief fixed damage path was landing `9`, but the client still rendered incoming hits as `nanobots / unknown damage` and the Thief did not visibly attack. Official live sends a `WeaponItemFullUpdate` for the Thief's QL1 Solar-Powered Pistol immediately after the Thief SCFU; AORebirth now announces captured equipped Subway NPC weapon definitions to the playfield after SCFU, but Mike's follow-up playtest proved that the prior generic weapon definition was still not client-accepted.
- The proven weapon-definition gap is the item-state shape and observer replay, not combat damage. Official captured `WeaponItemFullUpdate` packets for the Subway Thief's `121567` pistol and working armed Subway NPCs such as Violent Vagabond `130590` include live item energy and item timing stats: `Energy`, `AttackDelay`, and `RechargeDelay`. The previous AORebirth builder always sent `Energy=0` and omitted `AttackDelay`/`RechargeDelay`. Official player weapon updates also use `Energy=-1` when no finite energy value exists. The server now builds weapon definitions with live-shaped energy/timing stats and replays weapon definitions during existing-character visibility snapshots after SCFU and before `CharInPlay`, so already-spawned armed NPCs do not enter a client's visibility without weapon context.
- Mike's `2026-07-12` diagnostic live check proved the repaired Thief weapon context is client-accepted: the one enabled diagnostic hit rendered as `Thief hit you for 9 points of projectile damage.` The temporary Thief damage suppression and one-hit diagnostic gate are removed. The Thief keeps the captured packet envelope/timing (`AttackInfo` ammo `-1`, slot `6`, `Unk1=0`, normal hit type, weapon instance `0`, attack start/movement delays, and six-second repeat cadence), but damage now rolls from the equipped QL1 Solar-Powered Pistol `121567` item stats using the same current legacy weapon-roll input model as player normal weapon attacks instead of fixed captured `9` damage.
- Exact byte-vector validation proves the current message serializers can emit the official `20260711-170337` Thief `SpecialAttackWeapon`, `Attack`, and `AttackInfo` packet bodies byte-for-byte. Runtime delivery/order/client state has now been live-validated for Thief.
- The centralized damage-calculation system now has a side-effect-free core boundary in `ZoneEngine.Core.DamageCalculator`, with deterministic random input, request/result/trace models, evidence classifications, fixed captured damage representation, and legacy normal-hit preservation through `CombatDamageRules`. The ordinary weapon-input provenance audit selected Outcome B: partial input provenance. `WeaponDamageRequestBuilder` can now produce diagnostic-only request construction results, provenance records, and missing/malformed input issues, but it is not wired into active production damage. Repository fields expose weapon min/max, legacy damage bonus, raw damage type, timing, attack-skill dictionaries, some AMS caps, and damage-type AC/add-damage mappings, but local evidence still does not prove critical bonus source, critical-state resolution, Add All Off ordering, AMS-cap zero semantics, armor formula/order, or universal add damage. Production damage stays on the current legacy/captured boundaries; Subway Thief now uses the equipped QL1 Solar-Powered Pistol roll path validated by Mike's live playtest.
- The ordinary weapon-hit parity follow-up is evidence-only and keeps production damage unchanged. `WeaponDamageObservationValidator`, `WeaponDamageCandidateEvaluator`, `WeaponDamageParityReporter`, and the opt-in `WeaponDamageDiagnosticSnapshotBuilder` now support schema-backed observation validation, report-only candidate comparison, synthetic evaluator controls, and an initial underdetermined parity report. No live ordinary observations were fabricated. The Subway Thief live result validates the current Thief slice only and does not prove the full AO damage formula.
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

## Latest Subway Population Restore Validation

- Capture `20260710-202132` manifest regeneration: PASS; all 107 unique SCFU identities classified, exactly 29 supported-family and 9 ordinary rows included.
- `SubwayContentModuleRegistersCapturedNpcSpawnsWithoutOwningRuntimeSystems`: PASS.
- `SubwayExistingPopulationAndPatrolReplayRemainLoaded`: PASS.
- `SubwayOrdinaryArchetypesUseCaptureBackedTemplateFreeFramework`: PASS.
- `Subway20260710PopulationRestoreManifestMatchesCaptureAndBoundaries`: PASS.
- Full `PlayfieldLifecycleTraceTests`: 41/46 PASS; the five failures are the same pre-existing announcement/session/visibility architecture guardrail mismatches outside this population slice.
- `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS.
- Approved Chat/Login/Zone restart: PASS; expected ports listening.
