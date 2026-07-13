# AORebirth World Population Architecture

Status: global ordinary static-world foundation implemented.

```text
OrdinaryEnemyProfile -> WorldSpawnDefinition -> SpawnGroupDefinition
        |                         |
        +-> OrdinaryEnemyRuntimeService <- WorldPopulationController
                                             |
                                      WorldRespawnScheduler
```

## Ownership

- Profiles own reusable appearance, stats, movement, aggression, combat, corpse, and loot evidence; never coordinates or timers.
- `WorldSpawnDefinition` owns placement, stable source identity, profile/group/policy references, activation, quarantine, and provenance; never lifecycle code or loot generation.
- `SpawnGroupDefinition` owns membership, activation, and min/max-alive policy.
- `WorldPopulationController` owns activation, state transitions, lifecycle notifications, reset, cleanup, and diagnostics; never packets.
- `WorldRespawnScheduler` owns one keyed due-time collection ordered by due time, playfield, and spawn key. There is no timer per spawn.
- `OrdinaryEnemyRuntimeService` is the generic materializer. Combat, movement, visibility, loot-at-death, corpse protocol, and packets remain with established owners.

## State and respawn

`PopulationRuntimeState` separates configured identity from current runtime identity and records lifecycle state, timestamps, corpse identity, generation, and explicit failure.

```text
READY -> SPAWNING -> ALIVE -> DEAD_CORPSE_ACTIVE -> DESPAWNED
                                                      |
                                             WAITING_FOR_RESPAWN
                                                      |
                                                RESPAWNING -> ALIVE
```

Policies support none, fixed, bounded deterministic random, group-shared, scripted, and unresolved modes. Scripted and unresolved fail closed. Current Subway ordinary delay starts at final dead-NPC despawn, preserving the removed local scheduler semantics. Respawn uses the original spawn row and position. Runtime identity allocation remains unchanged; whether the client requires identity reuse is unresolved.

## Subway migration

`OrdinaryEnemyCatalog.GetSpawns()` adapts deterministically into normalized definitions. All 259 captured rows remain represented: 221 active at `PLAYFIELD_START` and 38 stored as `Quarantined=true`, `Enabled=false`. Thief and Filth Flea use the same controller and unchanged materializer. Quarantined rows cannot activate normally.

## Migration matrix

| Current path | Category | Owner/source | Replacement/status | Evidence |
| --- | --- | --- | --- | --- |
| Profile-backed Subway ordinary rows | STATIC_WORLD | catalog/captures | global controller; MIGRATED | Capture-backed |
| Ordinary respawn dictionary | STATIC_WORLD | ordinary runtime | shared scheduler; REMOVED_AFTER_PARITY | Existing despawn timing |
| DB `mobspawns` | STATIC_WORLD legacy | DB runtime | disabled normalized adapter; legacy owner retained | Legacy partial |
| Captured Arete robots | STATIC_WORLD candidate | robot orchestrator | ADAPTER_REQUIRED pending parity | Capture-backed |
| Vendors, terminals, static dynels | STATIC_OBJECT | specialized owners | STATIC_OBJECT_RETAIN | Existing contracts |
| Pets and summons | PET_OR_SUMMON | pet runtime | retained and validator-rejected | Owner-scoped |
| Scripted bosses/quests | SCRIPTED_ENCOUNTER | content handlers | CUSTOM_ENCOUNTER_RETAIN | Mechanic-specific |
| Dyna proposals | DYNA_CAMP | generated evidence | EVIDENCE_BLOCKED; no activation | Community documented |

## Lifecycle and persistence

Playfield startup activates once. Disposal/reset cancels schedules and clears runtime mappings while definitions remain reusable. Corpse removal is an explicit notification; population never examines loot or removes corpses. Ordinary state is ephemeral and repopulates after restart. `IPopulationStateStore` reserves a future durable boundary for camps, bosses, events, lockouts, and recoverable instances; no schema or persistence was added.

## Adding content

Profiles contain reusable enemy-type facts. Spawn rows contain exact identity and location evidence and reference a validated group and respawn policy. Use `PLAYFIELD_START` only for ordinary static enemies. Use an encounter controller for phases, waves, adds, hazards, special targeting, invulnerability, doors, quest transitions, or synchronization.
