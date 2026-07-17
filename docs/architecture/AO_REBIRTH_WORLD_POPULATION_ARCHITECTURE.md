# AORebirth World Population Architecture

Status: global ordinary static-world foundation implemented.

```text
OrdinaryEnemyProfile -> OrdinaryEnemySpawnDefinition -> WorldSpawnDefinition
        |                        |                         |
        |               fixed/range level model     SpawnGroupDefinition
        |                        |                         |
        +------------> OrdinaryEnemyRuntimeService <- WorldPopulationController
                                                          |
                                                   WorldRespawnScheduler
```

## Ownership

- Profiles own reusable appearance, movement, aggression, combat, corpse, and loot evidence; never coordinates or timers.
- `OrdinaryEnemySpawnDefinition` owns captured source stats, the generic fixed or inclusive-range level definition, level evidence/reroll metadata, exact placement, and the per-spawn respawn assignment.
- `WorldSpawnDefinition` owns normalized placement, stable source identity, classification, profile/group/effective-policy references, activation, quarantine, and provenance; never lifecycle code or loot generation.
- `SpawnGroupDefinition` owns membership, activation, min/max-alive policy, and
  an optional validated shared respawn-policy reference.
- `WorldPopulationController` owns activation, state transitions, lifecycle notifications, reset, cleanup, and diagnostics; never packets.
- `WorldRespawnScheduler` owns one keyed due-time collection ordered by due time, playfield, and spawn key. There is no timer per spawn.
- `OrdinaryEnemyRuntimeService` is the generic materializer. Combat, movement, visibility, loot-at-death, corpse protocol, and packets remain with established owners.

## State and respawn

`PopulationRuntimeState` separates configured identity from current runtime identity and records lifecycle state, timestamps, corpse identity, generation, selected level, and explicit failure.

```text
READY -> SPAWNING -> ALIVE -> DEAD_CORPSE_ACTIVE -> DESPAWNED
                                                      |
                                             WAITING_FOR_RESPAWN
                                                      |
                                                RESPAWNING -> ALIVE
```

The ordinary materializer resolves a level before constructing derived stats and combat state. Fixed definitions never consume randomness. An inclusive range uses an injected selector once for a new population generation, then stores that immutable selection in the runtime definition. Visibility loss/re-entry, combat reset, corpse transitions, route recalculation, and ordinary ticks cannot reroll it. A ranged row rerolls only when the controller creates a new respawn generation; a fixed row remains fixed.

The normalized model represents none, fixed, bounded-random, scripted, and
unresolved policies. The ordinary controller accepts enabled fixed or
bounded-random policies plus explicit no-respawn; scripted policies stay with
their encounter owners. `GroupSharedDelay` remains scaffold-only and is rejected
until synchronized group-timer semantics exist. Supported ordinary lifecycle
starts are death, corpse removal, and NPC despawn; a synthetic corpse-creation
timestamp is not invented. Unsupported and unresolved ordinary configurations
fail closed. PF127 eligible ordinary rows inherit a 240-second post-NPC-despawn
project policy. Resolution precedence is explicit per-spawn/archetype
assignment, explicit configured group assignment, then the ordinary default.
Explicit no-respawn remains no-respawn. Thief retains its explicit 60-second
policy; Filth Flea and Bloodcreeper retain explicit 240-second policies.

Policy registration rejects invalid modes, invalid lifecycle starts,
non-finite/out-of-range delays, and conflicting definitions that reuse one
policy key. Lifecycle scheduling is keyed by spawn and generation, so repeated
death or corpse-cleanup notifications cannot create duplicate pending work and
stale generations cannot respawn over a current runtime. If a supported death-
or corpse-removal-start timer matures before the dead NPC runtime is released,
the pending due time is retained and resumes at release instead of being lost.

The ordinary default does not apply to named enemies, bosses, scripted encounters, summons, pets, temporary encounter adds, vendors, static objects, containers, quest-owned entities, or unsupported classifications. Their established owners and explicit policies remain separate. The 240-second default is private-project policy, not proof of a universal official AO timer. Respawn uses the original spawn row and position. Runtime identity allocation remains unchanged; whether the client requires identity reuse is unresolved.

## Subway migration

`OrdinaryEnemyCatalog.GetSpawns()` adapts deterministically into normalized definitions. All 260 captured rows remain represented: 222 active at `PLAYFIELD_START` and 38 stored as `Quarantined=true`, `Enabled=false`. Thief, Filth Flea, and Bloodcreeper use the same controller and materializer. Bloodcreeper is the only current inclusive range (`L15..L25`); the other 259 rows remain fixed until evidence or an approved design decision establishes another range. Quarantined rows cannot activate normally, and this foundation does not change that boundary.

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

Profiles contain reusable enemy-type facts. Spawn rows contain exact identity and location evidence, an explicit fixed or evidence-backed range definition, and a respawn assignment that resolves through the validated policy precedence. Use `PLAYFIELD_START` only for ordinary static enemies. Use an encounter controller for phases, waves, adds, hazards, special targeting, invulnerability, doors, quest transitions, or synchronization. Future respawn captures identify exceptions or disputed timing; they are not required once per ordinary enemy to re-prove the private project default.
