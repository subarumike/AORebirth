# AORebirth Full Codebase Audit

## 1. Executive outcome

AORebirth has a credible shared ordinary-enemy runtime and a bounded dynamic-character visibility layer, but it does not yet have global loot, population, encounter, or restart-recovery services. The most important architectural defect is that `Playfield` remains the integration and state owner for corpse inventory, loot generation, credits, and several lifecycle transitions. The correct next foundation is a normalized loot service, followed by a playfield-independent population/respawn controller. No runtime behavior was changed by this audit.

## 2. Repository baseline

- Branch: `master`
- Starting HEAD and `origin/master`: `aa5f86e58771cf9e17d3d3b7d74da0e17b472ecb`
- Working tree: pre-existing modifications in ZoneEngine, AOtomation tests, PlayfieldLoader, and AOSharpLiveCapture; untracked `SubwayLootPoolRules.cs`, its test, and `Mission_Tables_Level_Restrictions_Teaming_Levels.ods`. They were preserved.

## 3. Audit scope

The audit searched server engines, core libraries, database entities/DAOs/schema files, ZoneEngine content and handlers, AOtomation tests, tools, capture analyzers, generated evidence, documentation, SQL staging, enemy catalog data, and relevant Git history. Third-party `msgpack-cli`, binaries, package caches, and raw client reverse-engineering exports were classified as dependencies/evidence rather than first-party runtime architecture.

## 4. Current architecture map

```text
LoginEngine -> character selection -> ZoneEngine/ZoneClient
ZoneClient -> Playfield -> PlayfieldRuntimeSystems facade
PlayfieldRuntimeSystems -> visibility, content, movement, combat, corpse and transfer services
OrdinaryEnemyCatalog -> OrdinaryEnemyRuntimeService -> NPCRuntimeService
Database DAOs -> DB mob/static/vendor materializers
Capture/import tools -> generated providers and evidence files (review boundary)
```

The facade extraction is valuable, but state and policy still flow back through `Playfield`, especially at `Playfield.cs:2988` and `Playfield.cs:3293-3856`.

## 5. Strong systems worth retaining

- `OrdinaryEnemyProfile`, `OrdinaryEnemyCatalog`, and `OrdinaryEnemyRuntimeService`: KEEP_AND_HARDEN. They establish the correct profile/spawn/runtime split and fail closed for bosses, summons, duplicate identities, and unresolved controlled values.
- `PlayfieldVisibilityInterestRuntimeService`, `PlayfieldSpatialCharacterIndex`, and bidirectional interest state: KEEP_AND_HARDEN. They remove the former unbounded character snapshot/fanout path.
- `PlayfieldRuntimeSystems` and extracted lifecycle services: KEEP_AND_HARDEN as seams, while moving remaining policy/state out of `Playfield`.
- Capture analyzers and deterministic content validators: KEEP_AND_HARDEN as evidence-to-proposal tooling.
- DAO/entity separation and content-module registration: KEEP, but do not treat the legacy schema as the target global content model.

## 6. Critical weaknesses

### AR-WP-001 — HIGH / ARCHITECTURE, OWNERSHIP

`Playfield.cs:3293-3856` owns table selection, random rolls, item construction, corpse inventory serialization, credits, transfer state, and cleanup scheduling. Failure mode: adding family, boss, mission, dyna, team, or event loot adds more branches to the playfield god object and makes loot policy inseparable from packet/lifecycle behavior. Correct by introducing `ILootDefinitionRepository`, `LootGenerationService`, `CorpseInventoryService`, and `LootRightsService`; gameplay data migration is required, packet guessing is not.

### AR-WP-002 — HIGH / DUPLICATION, CONTENT

Loot comes from captured profile evidence, debug tables, database `mobdroptable`, and a cleaning-robot-specific outcome path (`Playfield.cs:3293`, `3335`, `3394`, `3491`). Precedence is implicit. Failure mode: the same enemy can receive different semantics depending on route, and one observed outcome can become operational policy without normalized evidence metadata. Consolidate definitions behind one resolver and preserve source confidence.

### AR-WP-003 — HIGH / SCALABILITY, LIFECYCLE

Ordinary respawn is implemented by `OrdinaryEnemyRuntimeService.ScheduleRespawnAfterDespawn` while DB mobs, captured Arete robots, pets, player death, quest state, and future camps have separate lifecycle paths. There is no global scheduler, shared camp timer, restart recovery, or administrative reset contract. Implement a keyed scheduler and `PopulationStateStore`; keep ordinary delays as imported policy.

### AR-WP-004 — HIGH / DATA_MODEL

No normalized runtime model exists for `SpawnGroup`, `SpawnController`, `EncounterController`, `DynaCamp`, or dyna boss profile. The 174-row dyna import under `docs/generated/enemy_catalog/sources/dyna_boss_list_1.normalized.*` is evidence only. Activating it directly would conflate community coordinates/levels with proven runtime values.

### AR-WP-005 — HIGH / LIFECYCLE, PERSISTENCE

World population state, next respawn time, camp selection, raid/event lockouts, corpse state, and instance population have no durable restart contract. Ordinary static spawns may remain ephemeral, but boss/camp/event state needs explicit recovery rules before broad activation.

## 7. High-risk duplicated systems

- Ordinary catalog spawns versus `PlayfieldDbMobSpawnRuntimeService` versus `CapturedAreteRobotSpawnOrchestrator`: three population/materialization paths.
- Captured ordinary loot entries, DB drop tables, debug loot, and cleaning-robot outcomes: four loot-definition paths.
- Player, ordinary NPC, captured robot, and pet combat share some calculators/controllers but retain separate catalogs and lifecycle entry points.
- Arete mission state/content loaders are reusable in shape but currently Arete-scoped; world, dungeon, and generated mission population do not share them.
- Static dynels, statels/doors, vendors, and content modules use separate materialization/interaction routes with no global content manifest.

## 8. Missing global subsystems

IMPLEMENT: normalized loot repository/resolver; loot generation and rights; corpse inventory state; global spawn/group controller; respawn scheduler; encounter framework; dyna camp controller; instance/mission population adapter; population persistence/recovery; content dependency validator; deterministic simulation harness; operational population diagnostics.

## 9. Ordinary-enemy architecture status

The PF127 ordinary path follows the intended model: type profile -> placed definition -> shared runtime -> shared aggression/movement/combat/death/loot/respawn services. Remaining violations are not new per-enemy classes, but global policy leaking into `Playfield` and captured-provider-to-`CombatLootTableEntry` conversion. Arete robots remain a family-specific orchestrator and should migrate only after parity fixtures exist. Bosses and owned summons are correctly excluded from the ordinary catalog.

## 10. Boss and encounter architecture status

There is no general encounter framework. Custom code is justified only for phases, adds, hazards, scripted targeting/nanos, doors, quest progression, lockouts, waves, or synchronization. Health, damage, appearance, loot, and respawn differences are data. Existing named/quest handlers should eventually implement a narrow `IEncounterModule` contract and reuse shared combat, movement, corpse, visibility, loot, and spawn services.

## 11. Loot architecture status

Current loot is functional for narrow captured slices but structurally non-global. Adopt the model in `AO_REBIRTH_LOOT_ARCHITECTURE.md`. Deterministic assignment precedence is event/encounter override -> spawn override -> specific boss -> dyna family/level band -> enemy type -> family -> zone/dungeon/mission policy -> global defaults. At equal specificity, explicit priority then stable assignment key wins; ambiguous equal-priority assignments fail validation.

## 12. World-population architecture status

The ordinary profile layer is a strong seed, while placement, grouping, controller state, activation, and recovery remain fragmented. Adopt `EnemyTypeProfile`, `SpawnDefinition`, `SpawnGroup`, `SpawnController`, `EncounterController`, `RespawnPolicy`, and `PopulationState` from `AO_REBIRTH_WORLD_POPULATION_ARCHITECTURE.md`.

## 13. Dyna architecture status

The repository contains 174 normalized community-documented camp rows but no runtime dyna system. Treat names, coordinates, and approximate levels as source evidence with confidence, not RDB proof. A dyna boss normally uses ordinary combat plus a boss profile, `DynaCampController`, global loot, and a custom encounter module only for proven mechanics.

## 14. Respawn architecture status

Replace timer ownership scattered across population types with a keyed scheduler using due-time records and cancellation scopes (`playfield`, `instance`, `camp`, `spawn`, `owner`). Support fixed/range/shared/boss/minion/instance/quest/event/none policies. Persist only durable populations and future due times; rebuild ordinary ephemeral schedules from definitions.

## 15. Visibility architecture status

Dynamic characters now use bounded X/Z interest with 80-unit entry, 100-unit leave, and 32-unit cells. Entry ordering remains SCFU -> weapon definitions -> CharInPlay. Static dynels/vendors remain outside character interest. Remaining risks are immediate unpaced delivery, missing static-object interest, no formal load target, and global pool scans in `Playfield.cs:607,685` and `PetRuntimeService.cs:749` outside the central candidate path.

## 16. Persistence status

Persist: boss/camp alive state when downtime must not reset it, next durable respawn, selected dynamic variant, event/raid/loot lockouts, quest-controlled population, and instance metadata required for recovery. Keep ephemeral: ordinary spawn object identity, transient targets, movement/chase state, visibility membership, corpse packet handles, and ordinary restart repopulation unless evidence requires continuity.

## 17. Content-pipeline status

Current inputs include DB tables, manual C# providers, JSON quest/dialogue packs, DAT/RDB extraction, community documents, workbooks, captures, and generated reports. Standardize: source evidence -> normalized evidence record -> validated proposal -> review -> versioned runtime content -> loader. Reject duplicates, missing references, invalid coordinates/playfields, boss/summon misclassification, uncertain guaranteed loot, invalid QL ranges, and unbounded respawn values.

## 18. Testing status

The AOtomation source/contract suite provides valuable regression gates, particularly for packet order and ordinary/visibility behavior. Missing are executable contract tests for loot inheritance/rights, spawn groups, shared timers, camp recovery, instance population, restart recovery, encounter boundaries, and large-population integration. Some older tests are source assertions coupled to implementation names; prefer behavior fixtures and deterministic clocks/random sources.

## 19. Performance risks

- Immediate entry packet bursts remain proportional to selected visible entities; spatial selection bounds candidates but does not pace serialization or network queues.
- Timer-per-spawn expansion would scale poorly and complicate cancellation; use one scheduler heap/timing wheel per population domain.
- `Pool.Instance.GetAll<Character>` scans at `Playfield.cs:607,685` and `PetRuntimeService.cs:749` are global scans and should not enter per-tick paths.
- Database loot is loaded as complete arrays (`Playfield.cs:3491-3509`); acceptable at current scale, but normalize/index once assignments expand.
- Captured provider files are large generated C# arrays; prefer validated versioned data with indexed startup loading.

## 20. Removal candidates

See `AO_REBIRTH_REMOVAL_PLAN.md`. Highest-value candidates after migration are the cleaning-robot loot branch, debug loot runtime table, captured-provider loot conversion, obsolete schema files, `.orig` handler, and family-specific Arete robot orchestrator. None should be removed before replacement tests pass.

## 21. Implementation roadmap

1. Guardrails and deterministic test seams.
2. Global loot definition/resolution/generation, then corpse inventory and rights extraction.
3. Population model and keyed respawn scheduler.
4. Static world manifests and DB/ordinary adapters.
5. Dyna camps/boss profiles and dyna loot inheritance.
6. Mission/dungeon population adapters.
7. Scripted encounter modules.
8. Durable population/restart recovery.
9. Bulk content import, validation, diagnostics, and bounded rollout.

Visibility is already sufficiently established to be hardened in parallel; it is not the first missing foundation anymore.

## 22. Dependency order

`guardrails -> loot definitions -> corpse state/rights -> population model -> scheduler -> static adapters -> dyna -> mission/dungeon -> encounters -> persistence -> bulk activation`.

## 23. Evidence-blocked areas

Official visibility distance, corpse multi-user/reopen edge packets beyond captured flows, team/personal loot protocol, dyna exact stats/names/levels/respawn, mission generation rules, boss mechanics, camp restart semantics, raid lockouts, and full weapon/nano formula behavior remain evidence blocked. They must not silently default into gameplay.

## 24. Recommended next task

Implement audit guardrails plus a runtime-neutral normalized loot domain and deterministic resolver tests. Do not yet migrate live loot generation. This creates the contract needed to migrate existing DB/captured tables without changing gameplay.
