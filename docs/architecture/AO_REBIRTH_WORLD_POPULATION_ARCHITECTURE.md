# AORebirth World Population Architecture

## Core records

- `EnemyTypeProfile`: reusable appearance, stats, movement, aggression, combat, corpse, loot references, and evidence.
- `SpawnDefinition`: `SpawnKey`, profile key, playfield, position/orientation, optional level/scale/path/group overrides, respawn policy, enabled/quarantine, evidence.
- `SpawnGroup`: group key, playfield/zone/camp, min/max alive, activation policy, respawn policy, optional shared timer, population rules.
- `SpawnController`: activation, registration, alive/dead state, scheduling, reset, cleanup, and recovery.
- `EncounterController`: scripted mechanics only.
- `RespawnPolicy`: fixed/range/shared/boss/minion/instance/quest/event/none plus restart behavior.
- `PopulationState`: selected variant, live identities, death/due times, controller generation, durable version.

## Categories and owners

| Category | Owner |
| --- | --- |
| STATIC_WORLD | world manifest + spawn controller |
| DYNAMIC_WORLD | rule/selection provider + spawn controller |
| MISSION_GENERATED | mission population adapter |
| DUNGEON_STATIC | dungeon manifest + instance controller |
| DUNGEON_INSTANCE | instance population controller |
| QUEST_CONTROLLED | quest controller referencing spawn groups |
| EVENT_CONTROLLED | event controller referencing spawn groups |
| RAID_CONTROLLED | raid encounter controller and lockout store |
| DYNA_CAMP | dyna camp controller |
| DYNA_BOSS | boss profile selected by dyna camp controller |
| PET_OR_SUMMON | owner-scoped pet runtime, never static population |
| SCRIPTED_ENCOUNTER | encounter controller |

## Lifecycle

Definitions are immutable. On activation, the controller validates references and desired counts, asks the generic runtime to materialize characters, and records runtime identities separately. Death emits a population event. Final despawn schedules the referenced policy. Reset/disposal cancels by scope and removes identities through shared lifecycle/visibility hooks. Static providers never own timers.

The scheduler uses keyed due-time records rather than a timer per spawn. It supports independent/shared timers, random ranges from deterministic random sources, manual reset, cancellation, and restart recovery. Only durable camps/events/lockouts/instances persist; ordinary static population normally repopulates from manifests after restart.

## Requirements

Support fixed and multiple spawns, weighted/random profile pools, min/max alive, shared/independent timers, day/night/event/quest/faction conditions, scaling, activation, instance creation, restart restoration, disabled/quarantined rows, and diagnostic slices. All selections and precedence must be deterministic for a supplied seed.

## Migration

1. Adapt `OrdinaryEnemyCatalog` without changing active PF127 rows.
2. Adapt `PlayfieldDbMobSpawnRuntimeService` into legacy definitions.
3. Replace `CapturedAreteRobotSpawnOrchestrator` after parity fixtures.
4. Add static manifest loader and validators.
5. Add dyna/mission/dungeon adapters.

Visibility registration, combat, movement, corpse, and loot remain shared downstream services.
