# AORebirth Target Architecture

The target is a small set of global policy services with content supplied as validated data.

```text
Evidence -> Normalized content -> Validators -> Versioned content repository
                                              |
EnemyTypeProfile <- SpawnDefinition <- SpawnGroup <- SpawnController
       |                                      |
       +-> movement/aggression/combat         +-> RespawnScheduler
       +-> LootAssignmentResolver -> LootGenerationService
                                      -> CorpseInventoryService -> LootRightsService
EncounterController composes shared services only for proven scripted mechanics
PopulationStateStore persists durable camp/encounter/instance state
VisibilityInterestService bounds all dynamic-character delivery
```

Ownership rules:

- Profiles own reusable facts, never timers or live identities.
- Spawn definitions own placement and references, never live state.
- Controllers own state transitions, not packet formats or loot definitions.
- Combat owns damage/death signals, not loot selection.
- Loot generation creates an immutable result; corpse state owns remaining items/credits.
- Visibility owns recipients; packet serializers own wire shape.
- Persistence stores durable domain state, not transient controllers or client handles.
- Unsupported evidence is explicit and fails closed.

Runtime modules are reserved for phase transitions, adds, hazards, special targeting/nanos, waves, encounter doors, quest progression, lockouts, and synchronization. Elevated stats, appearance, loot, and respawn stay data-driven.

Architecture guardrails should reject enemy-name/MonsterData branches outside adapters, loot tables in enemy/controller classes, timers in spawn definitions, ordinary bosses/summons, guaranteed loot sourced only from an observation, duplicate identities/keys, invalid coordinates/ranges, unbounded timers, and whole-playfield character fanout.
