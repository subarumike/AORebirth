# Arete full-corpus completion report — 2026-07-31

## Result

This pass treated the complete surviving Arete repository and capture corpus as one evidence set. Raw packets were correlated with derived projections, reconciliation outputs, generated catalogs, fixtures, runtime code, tests, and prior verification artifacts. A behavior was not rejected merely because it occurred in another valid capture, preceded SCFU, used a regenerated runtime identity, lacked a closed loop, or was absent from one short live observation window.

The principal correction is that captured evidence now activates behavior from exact NPC metadata and the relevant lifecycle condition. It does not require the NPC to already be in the state that the evidence is supposed to start, and it does not require arbitrary 6 m activation or 2.5 m continuation distances.

## Evidence searched

### Capture inventory and cross-capture indexes

- `tools-temp/arete-analysis/capture_segment_index.md`
- `tools-temp/arete-analysis/capture_segment_index.json`
- `tools-temp/arete-analysis/arete_extraction_summary.md`
- Every Arete capture directory referenced by those indexes under `tools-temp/AOSharpLiveCapture/bin/Debug/captures/`, including the June interaction/quest/vendor sequence and the complete `20260722-104809` and `20260722-152454` combat, movement, lifecycle, and loot captures.
- Where present for a referenced capture: `capture_info.json`, `packets.hex.log`, `events.log`, `raw-packets.csv`, `movement-packets.csv`, `scfu-appearance.csv`, `enemy-combat.csv`, `enemy-state.csv`, `enemy-dossier.json`, `enemy-fight-events.log`, `enemy-respawns.csv`, `npc-lifecycle.csv`, `corpse-full-updates.csv`, `corpse-loot-observations.csv`, `vendor-full-updates.csv`, `shop-updates.csv`, interaction projections, chat projections, and system-message projections.

The two high-volume movement captures were read from these exact roots:

- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-104809/`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/`

### Derived and reconciled evidence

- `docs/generated/arete_20260722_104809_movement/`
- `docs/generated/arete_20260722_104809_movement_promotion_audit.md`
- `docs/generated/arete_20260722_152454_movement/`
- `docs/generated/arete_20260722_152454_movement_promotion_audit.md`
- `docs/generated/arete_full_corpus_movement_promotion_audit.md`
- `AORebirth/Server/ZoneEngine/Content/Captured/Arete/movement-full/manifest.json`
- `AORebirth/Server/ZoneEngine/Content/Captured/Arete/movement-full/{patrol,spawn,chase,flee,leash}.csv`
- `docs/evidence/arete-aggro-events-20260722-104809.csv`
- `docs/evidence/arete-aggro-events-20260722-152454.csv`
- `docs/evidence/ARETE_AGGRO_EVIDENCE_20260722_104809.md`
- `docs/evidence/ARETE_AGGRO_EVIDENCE_20260722_152454.md`
- `docs/evidence/ARETE_AGGRO_EVIDENCE_AGGREGATE_20260722.md`
- `docs/generated/arete_aggro_evidence_aggregate_manifest.json`
- `docs/generated/capture_backed_npc_combat_inventory.json`
- `docs/generated/capture_backed_npc_secondary_evidence_audit.json`
- `tools-temp/arete-analysis/vendor_observations.json`
- `tools-temp/arete-analysis/quest_chains.md`
- `tools-temp/arete-analysis/quest_chains.json`
- `tools-temp/arete-analysis/inventory_reward_evidence.json`
- Existing Arete extraction, framework, dialogue, quest, aggregate-validation, content-loading, and interaction reports under `docs/generated/`.

### Runtime, content, and tests

The audit included the Arete movement catalog/runtime, NPC runtime and leash policy, generated enemy-combat catalog, Arete spawn/lifecycle runtimes, global loot runtime, Arete dialogue and quest runtimes/content packs, vendor SQL, project content inclusion rules, and their focused tests. The primary files were:

- `AORebirth/Server/ZoneEngine/Core/Playfields/CapturedAreteMovementCatalog.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/CapturedAreteMovementRuntimeService.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/NPCRuntimeService.cs`
- `AORebirth/Server/ZoneEngine/Core/Navigation/NpcCombatLeashPolicy.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/CapturedEnemyCombatProfileCatalog.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/CapturedEnemyCombatProfileCatalog.g.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/AreteLandingSpawn.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/AlexAreaMobRuntime.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/LoreleiOasisMobRuntime.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/AreteAlienAreaMobRuntime.cs`
- `AORebirth/Server/ZoneEngine/Core/Playfields/GlobalLootRuntimeService.cs`
- `AORebirth/Server/ZoneEngine/Core/Arete/Dialogue/`
- `AORebirth/Server/ZoneEngine/Core/Arete/Quests/`
- `AORebirth/Server/ZoneEngine/Content/Arete/`
- `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/vendors.sql`
- `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/vendortemplate.sql`
- `AORebirth/Libraries/Source/AORebirth.Database/SqlTables/shopinventorytemplates.sql`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging.Tests/AreteFrameworkBootstrapTests.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging.Tests/CapturedAreteMovementRuntimeTests.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging.Tests/CapturedEnemyCombatProfileCatalogTests.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging.Tests/GlobalLootFoundationTests.cs`
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging.Tests/NpcChaseNavigationTests.cs`

## Evidence used and behavior promoted

### Identity and playfield binding

- Captured resource playfield `1044525` is mapped to runtime Arete playfield `6553` at the runtime boundary; captured rows retain the original playfield for provenance.
- Movement observation identities are scoped by `CaptureId`, `SourceIdentity`, and `SourceGeneration`. Regenerated source identities cannot collide across captures.
- Runtime eligibility binds exact family, template/MonsterData, level, name, and runtime playfield. Source identity is evidence provenance, not an invalid requirement that a newly spawned runtime NPC reuse an old capture identity.
- Movement preceding SCFU is retained when complete-capture lifecycle evidence resolves the same identity generation.

### Movement

The two complete captures reconcile deterministically to **24,042** independently classified observations:

| Decision | Observations |
| --- | ---: |
| Promotable | 20,573 |
| Ambiguous | 1,853 |
| Rejected | 1,616 |

The schema-4 aggregate collapses exact equivalents without combining unrelated routes and contains **20,267** runtime observations:

| Behavior | Source observations | Promotable | Ambiguous | Rejected | Deduplicated runtime rows |
| --- | ---: | ---: | ---: | ---: | ---: |
| Patrol | 19,874 | 18,693 | 0 | 1,181 | 18,402 |
| Spawn | 1,411 | 1,399 | 0 | 12 | 1,384 |
| Chase | 1,053 | 164 | 496 | 393 | 164 |
| Flee | 76 | 54 | 0 | 22 | 54 |
| Leash | 271 | 263 | 0 | 8 | 263 |
| Scripted | 1,357 | 0 | 1,357 | 0 | 0 |

Promoted runtime behavior:

- Patrol can begin for an exact eligible NPC from an idle controller; no pre-existing `Patrolling` state is required.
- Spawn, patrol, chase, flee, and leash use separate datasets and separate behavior-specific source-variant selection.
- The nearest exact-position variant cohort is selected deterministically per runtime spawn generation, with stable capture/source/generation tie-breaking. Selection never invents or joins routes.
- Exact coordinates, packet ordering, captured delay, path count, route signature, identity metadata, and playfield constraints are preserved.
- Timing uses the prior absolute due time, so delayed ticks catch up without accumulating clock drift.
- Terminal observations do not wrap into an invented loop. A completed route falls back to normal runtime behavior.
- Interruptions invalidate only the active behavior sequence. Spawn interruption completes spawn movement instead of replaying it; chase, flee, and leash only activate in their matching runtime lifecycle state.
- Combat and player influence are retained in chase, flee, and leash evidence. They do not contaminate clean patrol or spawn observations in the same route group.
- Leash evidence is required for an Arete actor before the existing safety reset policy may activate. The exact captured return route is then selected from the leash dataset.

The corrected analyzer no longer treats one packet's destination versus the next packet's start as teleport evidence. Only explicit set-position evidence, a recorded stop interruption, incomplete decode, or unresolved metadata produces the corresponding rejection.

### Automatic aggro

The combined July 22 projections contain **69** distinct enemy-to-player combat starts. **50** are NPC-first and prove automatic-aggro eligibility for **19** exact NPC metadata constraints. Fourteen constraints also have a directly measured lower-bound distance; five prove eligibility without an exact radius.

Measured lower-bound constraints promoted exactly:

| Exact constraint | Captured lower bound (m) |
| --- | ---: |
| Angry Minibull, template 30360, level 9 | 16.403593 |
| Angry Minibull, template 30360, level 10 | 16.225999 |
| Angry Minibull, template 30360, level 12 | 13.393116 |
| Angry Minibull, template 30360, level 13 | 16.192275 |
| Cleanmeister Intelligence Robot, template 297023, level 2 | 0.847455 |
| Desert Reet, template 30365, level 5 | 1.582614 |
| Desert Reet, template 30365, level 6 | 23.167874 |
| Garbage Flea, template 17657, level 2 | 15.576482 |
| Lolly the Reet, template 30365, level 10 | 56.243689 |
| Robotic Guard Dog, template 17720, level 13 | 10.535007 |
| Rollerrat, template 17687, level 5 | 16.639269 |
| Rollerrat, template 17687, level 6 | 18.747485 |
| Supreme Collector of Waste, template 17714, level 4 | 9.240783 |
| Waste Collector, template 17714, level 2 | 1.332759 |

The following exact constraints have proven automatic eligibility but no measured radius: Angry Minibull level 8, Garbage Flea level 1, Gnarl the Roller level 7, Kneebreaker Alfonzo Rizzolo level 4, and Violent Protester level 3. Runtime consumes that proof using a **1.0 m contact-only floor**. This promotes the proven direction of behavior—NPC-first automatic combat—without claiming that 1.0 m is the original sight radius or probability.

### Captured attack chains

The generated Arete combat profiles were audited against SCFU, WIFU, SAW, Attack, AttackInfo, damage type, slot, level, and lifecycle evidence. The stale catalog rule that rejected capture-safe Arete `FixedAttackOnSight` natural contracts merely because they did not use production-specialized values was removed for exact eligible profiles.

`Violent Protester` is bound as family 103, MonsterData 203740, level 3 in PF 6553 with its exact captured appearance/position and the capture-safe equipped WIFU → SAW → Attack → AttackInfo profile `f2059f604ad30393-e92b5cbb00b5ff5f` from `20260614-024525`. Its NPC-first eligibility is independently proven by `20260722-152454`.

The correction makes 43 capture-safe generated Arete profiles eligible under their exact metadata and captured stream constraints. Representative coverage includes Flea, Rat/Gnarl, Waste/robot, Minibull, Docker, and Reet families. Alternate stream order and profile-selection probability remain capture-controlled; the runtime does not invent them. The focused combat integration set passed 54/54 before the final scoped-snapshot validation below.

### Spawn and lifecycle timing

Measured lifecycle values are applied only to exact actors/ordinary families that the evidence identifies:

- `32-V Docker`: 40-second replacement; other Docker-like named variants retain their existing fallback because their exact lifecycle rows are incomplete.
- Ordinary Desert Reet and Rollerrat groups in the Lorelei area: 40 seconds.
- Ordinary Rollerrat and exact Angry Minibull groups in the alien area: 40 seconds.
- Kneebreaker Alfonzo Rizzolo: 26.923 seconds from the `20260722-152454` death-to-replacement observation.
- Violent Protester: deterministic 19.958-second median of clean replacement observations across `20260722-104809` and `20260722-152454`.

Named or moving/grouped slots were not assigned a captured timer merely because another member of the broad family had one.

### Loot and corpse lifecycle

- The Arete loot runtime now binds runtime PF `6553` while retaining captured PF `1044525` in evidence provenance. This makes the previously unreachable exact Arete definitions eligible in the live runtime namespace.
- Every identity-linked atomic corpse snapshot in the two July 22 `corpse-loot-observations.csv` projections is represented without mixing items across corpses.
- The `20260722-104809` ordinary corpus contributes exactly 1 Docker, 14 Waste Collector, 11 Garbage Flea, and 15 Cleaning Robot snapshots: **41 atomic snapshots**.
- The `20260722-152454` corpus contributes exact Docker, Waste Collector, Cleanmeister, Supreme Collector, Rollerrat, Desert Reet, Angry Minibull, Gnarl, and Kneebreaker snapshots, including empty-but-openable corpses and exact credits.
- Two blank-name rows were resolved from complete-capture identity evidence instead of discarded: owner `798911CF` is Supreme Collector of Waste (MonsterData 17714), 35 credits plus seven items; owner `79891585` is Gnarl the Roller (MonsterData 17687), zero credits plus seven items.
- Snapshot selection remains atomic and deterministic for a supplied seed. No drop-rate or unseen wider-pool claim is made.

### Vendors and interactions

The vendor corpus contains nine complete VendorFull + ShopUpdate vendors with 320 unique captured stock rows, plus 15 VendorFull-only observations. Existing complete mappings were audited against the runtime SQL.

The missing Bronto Burgers vending-machine mapping is now exact:

- captured vendor identity `VendingMachine:12D1BF27` followed by statel `0xC00E1999`;
- runtime vendor id `429457422`, PF `6553`, template `121036`;
- vendor hash `ARBRTBG`, inventory hash `BRBG`;
- ten QL1/count1 rows in captured ShopUpdate order: `130621`, `130593`, `130623`, `130624`, `130581`, `130612`, `130625`, `130606`, `130602`, `130603`.

Quest and interaction evidence was correlated through `capture_segment_index`, `quest_chains`, `inventory_reward_evidence`, the captured dialogue JSON, current quest runtimes, and exact interaction catalog/runtime. Existing capture-backed flows retained include the Flint → Stan → Sarah → Vernon → Doctor Mason → Lorelei → Vaughn chain; Antonio, Karli, Leonora, Patrick, Remi, and Shiny Sword flows; Rex/Marcus progression; Bill/kneecapping; shipping-manifest; and captured exact NPC interaction/trade branches. Proven mission identities, actions, inventory changes, cash/XP deltas, chat packets, and lifecycle events remain authoritative; missing prompt text or a reward-selection rule is not filled by guess.

All **48** extracted mission-state groups are reconciled in `docs/evidence/ARETE_MISSION_STATE_RECONCILIATION_20260731.md`: 8 same captured identities are already consumed by Rex (3) and Marcus (5); 35 regenerated June identities are superseded by later named packet-aware captures and consumed by the current Flint, Alex, Bill, Stan, Sarah, Antonio, Vernon, Shipping, Lorelei, Vaughn, and Patrick runtimes; and 5 terminal-only observations remain genuinely incomplete for activation. Forty-seven groups prove `Action 59 (P1=56003, P2=mission instance)` plus same-timestamp quest deletion; group `B1C5` proves deletion only. The reconciliation found zero internal contradictions and no additional safe quest activation to implement.

The six previously unconsumed interaction slices are now explicit evidence-only runtime content. Barry the Food Vendor, Boris the Peacekeeper, and Desmond Calitri load their exact captured roots/options and trade direction while missing prompt/index semantics remain closed. Mario Carles exposes the finite sequence of 27 captured direct-interaction replies; Robotic Guard Dog exposes `Woof woof woof!!!!`; Shady Guy exposes its three exact replies, including `Useless..`. Mario's two separately observed `No you!` shouts remain outside direct-reply runtime because their trigger/audience semantics are absent. Runtime identities are not eligibility keys, and the finite reply sequences do not invent a repeat policy after captured evidence ends.

## Observation traceability

Ambiguous and rejected movement rows remain in the per-capture behavior CSVs with confidence, influence, geometry, identity-resolution source, and exact reasons. Rejected rows cannot contaminate clean rows that share a route group.

Ambiguous reason incidences:

| Exact reason | Incidences |
| --- | ---: |
| `scripted_family_heuristic_only` | 1,357 |
| `post_combat_direction_not_leash` | 429 |
| `combat_target_position_unavailable` | 62 |
| `combat_direction_ambiguous` | 5 |

Rejected reason incidences:

| Exact reason | Incidences |
| --- | ---: |
| `metadata_unresolved` | 1,185 |
| `path_interrupted_by_stop_command` | 536 |
| `explicit_setpos_teleport` | 146 |
| `combat_target_position_unavailable` | 62 |
| `post_combat_direction_not_leash` | 26 |
| `combat_direction_ambiguous` | 2 |

Reason counts are incidences; one observation may carry more than one exact reason.

## Historical live-verifier caveat

The packet observations in these files remain valid historical evidence:

- `docs/evidence/ARETE_MOVEMENT_LIVE_VERIFICATION_20260731.md`
- `docs/evidence/ARETE_MOVEMENT_LIVE_VERIFICATION_20260731_BASELINE.md`
- `docs/evidence/ARETE_MOVEMENT_LIVE_VERIFICATION_20260731_POST_FIX.md`
- their companion identity/path CSVs under `docs/evidence/`.

Their old eligibility fields based on a 6 m activation gate and 2.5 m continuation gate are superseded. `tools-temp/AOSharpCaptureAnalyzer/verify_arete_movement_runtime.py` now loads schema-4 `CaptureId`, matches exact metadata, chooses a patrol-specific source variant without those distance gates, and treats “promoted patrol but no packet in this observation window” as a neutral window result—not proof that the captured route is invalid. Exact observed matches, including the recorded Garbage Flea patrol match, remain evidence.

## Explicit remaining gaps

Only the following facts remain unsupported or contradictory after the full search. Each row names the evidence searched and the exact boundary retained.

In this table, “both July 22 captures” means the named file in each of these exact directories: `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-104809/` and `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/`. “Arete analysis” means the exact repository directory `tools-temp/arete-analysis/`.

| Missing fact | Evidence searched | Why it remains genuinely unsupported | Runtime boundary |
| --- | --- | --- | --- |
| Scripted movement trigger semantics | Both July 22 captures' `movement-packets.csv`, `enemy-combat.csv`, `enemy-state.csv`, `npc-lifecycle.csv`, `scfu-appearance.csv`; both generated `scripted.csv`; both movement manifests and audits; aggregate manifest | 1,357 observations can be classified only by a scripted-family heuristic. The initiating script/event and replay contract are not present. | Scripted rows remain out of runtime. Other behavior classes are unaffected. |
| Original patrol-variant probability/distribution | Both per-capture movement datasets/manifests and the schema-4 aggregate | The corpus proves multiple exact variants but not an original random weighting law. | Runtime chooses an exact captured variant deterministically per spawn generation; no route is invented. |
| Exact leash activation threshold | Both July 22 captures' generated `leash.csv`, raw movement/combat/lifecycle projections, `AORebirth/Server/ZoneEngine/Content/Captured/Arete/movement-full/leash.csv`, `AORebirth/Server/ZoneEngine/Core/Navigation/NpcCombatLeashPolicy.cs` | Captures prove return direction and exact route/timing, not the unseen server's distance/time threshold. | Only actors with exact leash evidence may use the existing 100 m safety reset; captured return routes activate after that matching condition. The 100 m value is not represented as captured. |
| Exact automatic-aggro radius for five constraints | Both aggro event CSVs/reports, aggregate aggro report/manifest, raw combat/movement/state/SCFU projections, runtime aggro catalog | NPC-first attack proves eligibility, but no measurable NPC-first start distance exists for Angry Minibull L8, Garbage Flea L1, Gnarl L7, Kneebreaker L4, or Violent Protester L3. | Eligibility is implemented with a 1 m contact-only floor; exact sight radius remains explicitly unknown. |
| Kneebreaker attack packet chain | `docs/generated/capture_backed_npc_combat_inventory.json`; generated combat catalog; both Arete aggro reports and event CSV; `20260722-152454/enemy-dossier.json`, `enemy-fight-events.log`, `npc-lifecycle.csv`, `corpse-full-updates.csv`, `corpse-loot-observations.csv`, and `enemy-respawns.csv` | Identity, corrected family 137, SCFU, lifecycle, NPC-first eligibility, death, respawn, and loot are present, but no exact Kneebreaker WIFU → SAW → Attack → AttackInfo chain/generated profile exists. | Spawn, automatic eligibility/contact acquisition, lifecycle, and loot are implemented; packet emission stays quarantined rather than borrowing another NPC's attack contract. |
| Official loot snapshot probabilities and unseen wider pools | Both July 22 `corpse-loot-observations.csv`, `corpse-full-updates.csv`, lifecycle/identity projections, global loot runtime, focused loot tests | Every observed atomic snapshot is consumed, but observations do not reveal selection probability or guarantee that no unseen snapshot exists. | Select only complete captured snapshots atomically; keep probability evidence marked unresolved. |
| Stock for 15 VendorFull-only identities | `tools-temp/arete-analysis/vendor_observations.json`, `capture_segment_index.md`, `capture_segment_index.json`, `arete_extraction_summary.md`, referenced `vendor-full-updates.csv` and `shop-updates.csv`, vendor SQL | Exact vendor identity/template/geometry exists, but no ShopUpdate stock rows bind to these 15 observations. | Do not invent inventory. Nine complete vendors and their 320 unique rows remain usable; Bronto's exact ten-row stock is promoted. |
| Exact respawn timer for contaminated or ambiguously bound groups | Both July 22 captures' `enemy-respawns.csv`, `scfu-appearance.csv`, `npc-lifecycle.csv`, `enemy-dossier.json`, corpse/death and spawn projections; `AlexAreaMobRuntime.cs`, `LoreleiOasisMobRuntime.cs`, `AreteAlienAreaMobRuntime.cs`, `AreteLandingSpawn.cs` | Flea intervals 136.212/1853.554 s and Supreme 1711.404 s cross missed generations/contamination. Waste, Cleaning Robot, and Burning groups have clean ranges but moving/grouped slot association is not exact. | Exact actor/group timers listed above are implemented; unresolved groups retain existing fail-closed fallback timers rather than adopting a misleading aggregate. |
| Bill Surveillance Uplink XP selection/scaling rule | `tools-temp/arete-analysis/quest_chains.{md,json}`, `inventory_reward_evidence.json`, Bill dialogue/quest captures, `SurveillanceUplinkQuestRuntime.cs`, later quest verification artifacts | Two exact observations conflict: 2076 XP and 2229 XP; both pair with the same exact 1160 credits. The corpus does not expose the level/scaling/branch rule selecting between them. | Exact 1160 credits are retained. The implemented 2076-XP branch matches its captured FormatFeedback wire; 2229 is preserved as contradictory evidence, not silently discarded. No generalized scaling formula is claimed. |
| Five terminal-only mission observations | `docs/evidence/ARETE_MISSION_STATE_RECONCILIATION_20260731.md`; `tools-temp/arete-analysis/quest_chains.{md,json}`; capture segment indexes; June interaction/system projections; all current Arete quest runtimes | Mission instances `5514B270`, `5514B273`, `5514B275`, `5514B277`, and `5514B285` prove only terminal Action 59/deletion. Title, objective, source NPC, accept transition, and completion trigger are absent across the searched corpus. | Preserve the exact terminal evidence in the reconciliation; do not create an activatable quest from a terminal-only packet. |
| Missing June interaction semantics | `tools-temp/arete-analysis/dialogue_trees.md`; capture segment indexes; June interaction/chat/system projections; `captured-june-interactions.dialogue.json`; exact interaction catalog/runtime/tests | Barry/Boris/Desmond have exact roots, option sets, branch order, and applicable trade direction, but some prompt bodies/index semantics are absent. Mario has 27 exact direct replies plus two observed `No you!` shouts whose trigger, audience/broadcast mode, and repeat rule are absent. | Expose only exact options/direction and the 27 direct replies; omit absent prompt/index claims, keep the two unbound shouts out of runtime, and stop finite sequences when evidence ends. |

This list does not treat an explicit teleport, interrupted path, or incomplete packet as an unresolved promotable route. Those are recorded rejection outcomes. It also does not treat the absence of a packet in one short live window as absence of evidence when another complete capture already proves the behavior.

## Validation

- Movement aggregate self-tests (`tools-temp/AOSharpCaptureAnalyzer/test_aggregate_arete_movement_runtime.py`): **PASS, 5/5**.
- Corrected movement-audit self-tests (`test_audit_movement_promotion_candidates.py`): **PASS, 9/9**.
- Corrected movement aggregate reproducibility check: **PASS**.
- Corrected live-verifier `--self-test`: **PASS**.
- Aggro aggregate self-test and reproducibility check: **PASS** (`69` events, `50` NPC-first, `19` constraints, `14` measured lower bounds).
- Focused `CapturedAreteMovementRuntimeTests`: **PASS, 12/12**.
- Focused `NpcChaseNavigationTests`: **PASS, 39/39**.
- Focused `CapturedEnemyCombatProfileCatalogTests`: **PASS, 54/54**.
- Focused `CapturedEnemyCombatActiveCoverageTests`: **PASS, 4/4**; generator check also passed with `1,583` audited actors, `313` certified and `1,270` explicitly unresolved.
- Focused `GlobalLootFoundationTests`: **PASS, 14/14**.
- Focused `AreteFrameworkBootstrapTests`: **PASS, 9/9**.
- Focused `CapturedAreteExactInteractionTests`: **PASS, 2/2**.
- Full relevant AOtomation messaging set: **PASS, 134/134** across the seven focused classes above.
- Repository-wide AOtomation diagnostic snapshot: `888/926` passed. The `38` remaining failures are outside the seven focused Arete classes and include preserved damage-formula expectation changes, deployment-fixture content-copy assumptions, PF127 population-count drift, and the pre-existing pet visibility-hook assertion. No focused Arete test failed.
- Approved debug build (`tools/build_aorebirth_debug.cmd`): **PASS** after the documented stop-before-build lifecycle released the running executable.
- Engine restart (`restart-engines.cmd`): **PASS**; Chat ports `6996` and `7012`, Login port `7500`, and Zone port `7501` all reported listening.
- Commit and `origin/master` push: **PASS in the scoped snapshot containing this report; the final task handoff records the immutable commit hash.**

## Evidence-discipline confirmation

No available Arete evidence was ignored because of an invented rule. In particular, this pass did not require a pre-existing runtime state, one packet subtype where correlated lifecycle packets proved the same fact, one specific capture session, a closed patrol loop, repeated edges, multiple identities, the reuse of a capture-time identity at runtime, a packet inside one short observation window, or the absence of combat/player influence from the behavior classes where that influence is expected.

Proven exact values were implemented as exact values. Proven ranges or medians were identified as derived values. Proven eligibility was implemented even when exact radius remained unknown. Contradictory probabilities, scaling rules, and absent packet chains remain explicit gaps rather than guessed defaults.
