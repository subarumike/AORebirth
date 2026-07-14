# Generic Ordinary-Enemy Runtime

Population ownership: profile-backed Subway rows are activated and respawned by `WorldPopulationController`. `OrdinaryEnemyRuntimeService` materializes requested rows only and no longer enumerates population or owns respawn timers. The catalog remains the capture-backed adapter with 259 rows, 221 active and 38 quarantined.

## Decision

An ordinary enemy is an `OrdinaryEnemyProfile` plus one or more
`OrdinaryEnemySpawnDefinition` rows. Ordinary enemies do not receive their own
spawn class, AI class, combat loop, corpse handler, or respawn handler.

Custom C# encounter modules are reserved for named bosses, owned summons, and
scripted mechanics that cannot be represented by the validated profile model.
The ordinary catalog rejects those categories rather than silently treating
them as normal enemies.

## Normalized data model

`OrdinaryEnemyProfile` contains stable type data:

- stable profile and family keys;
- display name and `MonsterData`;
- template-backed or captured-direct construction;
- appearance, SCFU variant, texture, and mesh data;
- aggression, chase, and return policy;
- combat mode, damage source, visible-weapon policy, and captured combat contract;
- loot and credit evidence;
- corpse packet and lifetime policy;
- evidence references and explicit boss/summon exclusion flags.

`OrdinaryEnemySpawnDefinition` contains placed-instance data:

- stable spawn key and captured source identity;
- profile key and playfield;
- level, health, health damage, scale, and run speed;
- exact position and orientation;
- static, patrol, or captured-route movement data;
- exact captured SCFU overrides when present;
- explicit respawn evidence and delay;
- active or quarantined runtime disposition;
- capture, timestamp, and owner provenance.

Unknown combat, loot, credit, movement, and respawn evidence remains explicit.
It is not converted into a zero, false, guaranteed drop, guessed delay, or
working combat contract. The validator rejects duplicate profile keys, spawn
keys, source identities, missing profile references, invalid controlled values,
owned spawns, and scripted/boss rows.

## Controlled behavior values

- Aggression: `Passive`, `Retaliate`, `Auto`, `Scripted`, or explicit `Unresolved`.
- Movement: `Static`, `Patrol`, `Roam`, `Scripted`, or explicit `Unresolved`.
- Combat: unarmed melee, natural melee, equipped melee, equipped ranged, nano,
  hybrid, scripted, or explicit unresolved.
- Damage: captured fixed, weapon roll, profile range, natural attack, scripted,
  or explicit unresolved.
- Loot evidence: guaranteed proven, observed available, profile inherited,
  none proven, or unresolved.

Scripted modes are modeled so imports can classify them, but the ordinary
runtime validator rejects them and directs them to a custom encounter module.
Random roaming is not guessed. A `Roam` row requires captured waypoints and uses
the shared waypoint movement path until stronger behavior evidence exists.

## Runtime ownership

`OrdinaryEnemyRuntimeService` is the only PF127 ordinary spawn coordinator. It:

1. selects enabled rows from `OrdinaryEnemyCatalog`;
2. constructs a template-backed or captured-direct `Character`;
3. applies profile stats, appearance, movement, and combat data;
4. registers the normalized runtime definition;
5. preserves direct-spawn packet order: SCFU, then visible weapon definitions;
6. delegates visibility replay to the existing visibility services;
7. prevents duplicate source registration;
8. schedules evidence-backed respawns and retries failed respawns after five seconds;
9. removes runtime state during final despawn.

Existing services retain their established responsibilities:

- `NPCRuntimeService`: lifecycle coordination, target acquisition, combat, death,
  cleanup, and ordinary-runtime delegation.
- `CapturedEnemyCombatRuntime` and `NpcCombatTickCoordinator`: weapon setup,
  attack timing, attack packets, and damage-source selection.
- `NPCController` and `PlayfieldNpcCombatMovementRuntimeService`: patrol, chase,
  follow, and return movement.
- `NpcCorpseLifecycleCoordinator`, playfield corpse access, and timed lifecycle
  services: death, corpse materialization, loot access, and despawn.
- visibility packet and fanout services: SCFU, weapon definitions, `CharInPlay`,
  and client delivery.

No packet layout or global visibility batching/fanout behavior is changed.

## Audited implementation map

| Classification | Current owner |
| --- | --- |
| Generic data | `OrdinaryEnemyProfile`, `OrdinaryEnemySpawnDefinition`, `OrdinaryEnemyCatalog` |
| Generic runtime | `OrdinaryEnemyRuntimeService`, `NPCRuntimeService`, `NpcCombatTickCoordinator`, `NPCController` |
| Family/enemy data | captured supported and generated ordinary providers, adapted into normalized profiles |
| Family/enemy runtime | retired for ordinary Subway enemies |
| Boss/scripted runtime | separate content/encounter modules only; no Subway boss is routed through the ordinary catalog |
| Visibility infrastructure | existing playfield visibility sequencing/fanout and PF127 diagnostics |
| Capture import | `generate_subway_ordinary_content.py` |
| Tests | AOTomation messaging lifecycle/profile tests |

The audit found duplicate spawn construction, stat/appearance application,
movement setup, runtime registration, packet announcement, and respawn ownership
in `CapturedSubwaySpawnOrchestrator` and
`CapturedSubwayOrdinarySpawnOrchestrator`. Both runtime files are removed. The
captured providers remain evidence/data inputs; packet serializers and lifecycle
services remain their established owners.

## Subway migration

The catalog normalizes all existing supported-family and generated ordinary
Subway evidence into 17 reusable type profiles and 259 exact spawn rows:

- supported profiles: Filth Flea, Discarded Pet, Disobedient Bot, Mugger, Thief,
  and Violent Vagabond;
- generated ordinary profiles: Shadow, Stim Fiend, Workman Striker, Architect
  Striker, Workman, Architect, Looter, Deranged Shopper, Infector, Striker, and
  Lost Thought.

The existing safe activation boundary remains 221 active rows. The 29
supported-family and 9 generated ordinary rows in the PF127 diagnostic slice
remain present as data but quarantined by default. Profile or spawn existence
does not enable a row.

Named bosses and owned summons are not in the catalog.

## Thief parity

Thief now uses the shared profile/runtime path while preserving the accepted
captured values: source identity `0x7953AEA5`, template `A051`, level 5, health
115, scale 93, run speed 20, exact position, captured appearance/SCFU bytes,
retaliate aggression, captured patrol replay, QL1 Solar-Powered Pistol `121567`
in the right hand, weapon-derived damage, captured attack timing/context,
captured corpse packet/CATMesh, guaranteed QL1 Stolen Handbag `297055`, one-second
fully-looted cleanup, five-minute unlooted lifetime, and 60-second post-despawn
respawn.

## Filth Flea parity

Filth Flea uses the same runtime with template `A096`, per-spawn level/health/
position/run-speed rows, retaliate aggression, captured patrol replay where
present, the captured opening poison and repeating natural-melee sequence, exact
SCFU material override, exact corpse packet, observed item loot, observed credit
range `29..79`, and 240-second post-despawn respawn.

The combat tick no longer identifies Filth Flea by name or `MonsterData`.
Opening and repeating special attacks are generic captured-contract data.

## Capture-to-profile workflow

`tools-temp/AOSharpCaptureAnalyzer/generate_subway_ordinary_content.py` is the
review boundary for future captured ordinary rows.

```text
python tools-temp/AOSharpCaptureAnalyzer/generate_subway_ordinary_content.py --check
python tools-temp/AOSharpCaptureAnalyzer/generate_subway_ordinary_content.py --write
```

`--check` is the safe default. It builds and validates in memory, compares the
canonical generated content with the checked-in provider, and writes nothing.
`--write` validates first and atomically replaces changed output. Generation is
deterministic and fails closed for identity/profile collisions, missing profile
references, named bosses, owned summons, malformed or unsupported rows, and
unresolved combat values rendered as concrete capture evidence.

Generated rows remain reviewable and do not become enabled merely because they
were emitted.

## Adding an ordinary enemy

1. Finish a comprehensive live capture and run the analyzer in `--check` mode.
2. Review identity, placement, appearance, movement, combat, loot, corpse, and
   respawn evidence. Leave unknowns explicit.
3. Add or reuse one mechanically accurate type profile.
4. Add exact spawn rows referencing that profile.
5. Keep new rows quarantined until the accepted-enemy coverage and runtime gate
   pass.
6. Run profile, population, combat, movement, corpse/loot, respawn, visibility,
   generator, and ZoneEngine validation.

No enemy-specific runtime class is added.

## Adding a scripted boss

Create a dedicated content/encounter module only when capture evidence proves a
mechanic cannot be represented by the ordinary profile. Keep its spawn and
script ownership outside `OrdinaryEnemyCatalog`; reuse shared combat, movement,
corpse, loot, and visibility services where applicable.

## Visibility boundary and unresolved evidence

The PF127 existing-character visibility-volume problem remains a separate global
visibility-layer task. This runtime does not add distance limits, packet budgets,
batching, throttling, delayed SCFU, pagination, acknowledgements, or per-enemy
visibility suppression. Enabling many quarantined rows can still exercise that
known boundary.

Current unresolved data remains fail-closed, including combat sources without a
landed captured hit, ordinary respawn delays not yet captured, automatic-aggro
radii not yet captured, and random roam behavior not proven by movement evidence.
