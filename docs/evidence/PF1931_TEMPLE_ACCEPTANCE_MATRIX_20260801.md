# PF1931 Temple of Three Winds acceptance matrix - 2026-08-01

> **Superseded status:** The sole current Temple status authority is
> `TEMPLE_FULL_CORPUS_COMPLETION_20260801.md`. This document remains evidence
> and provenance only.

## Authority and closure rule

This is the sole authoritative status document for PF1931 Temple of Three
Winds gameplay. Earlier Temple documents remain evidence/provenance only. If
an earlier completion, blocker, or test count conflicts with this matrix, this
matrix wins.

Temple is complete for the existing evidence corpus when every recoverable
contract has a production owner and regression test, every unsupported
contract is explicitly fail-closed, and no fail-closed row creates a timer,
actor, modifier, loot result, corpse reroll, packet result, or runtime identity
mapping.

## Evidence boundary

The closure audit used:

- the complete `tools-temp/AOSharpLiveCapture/bin/Debug/captures` corpus,
  including all 32 PF1931 metadata-selected sessions, the PF647 boundary
  session `20260722-041602`, and the three legacy raw-hex Temple sessions;
- generated capture inventory, active combat coverage, semantic profile,
  population, level, QL, loadout, loot, lifecycle, and movement projections;
- exact-byte packet fixtures and decoded N3Teleport, PlayfieldAnarchyF,
  DoorStatusUpdate, GenericCmd, ChestFullUpdate, combat, nano, corpse, and
  inventory rows;
- official `playfields.dat`, RDB playfield resource `1931`, tilemap resource
  `1930`, and
  `Content/Official/TempleOfThreeWinds/pf1931-dungeon-geometry.json` source
  SHA-256 `759754a064c5740000bc2168a00bfc267b31f32d51fe331a69dfd592c3466804`;
- `items.dat`, client `18.8.62_EP1` nano data (`10,965` decoded nanos), and
  the existing mapped client/statel/nano routines; and
- all PF1931 evidence reports listed in the provenance section below.

No new capture was requested or run. The commits between the prior deployed
baseline `7558f69b` and this audit's synchronized start contain no PF1931
content or contract addition. Shared-runtime changes are accepted only through
the regressions named below.

## Acceptance totals by subsystem

| Subsystem | Accepted complete | Intentionally fail-closed | Production owner | Acceptance tests |
|---|---:|---:|---|---|
| Population and spawns | `167/167` ordinary actor slots; `14/14` named lifecycle/combat domains | `0` active slots | `TempleOfThreeWindsContentModule`, `CapturedTempleOfThreeWindsContentProvider`, `OrdinaryEnemyCatalog`, `CapturedTempleOfThreeWindsEncounterRuntimeService` | `TempleOfThreeWindsOrdinaryContentTests`, `DungeonNamedEncounterCompletionTests`, `DungeonNamedLifecycleCompletionTests`, `OfficialDungeonNavigationTests` |
| Ordinary combat | `167/167` PF1931 actors resolve exact active contracts | Inactive level/loadout combinations remain non-selectable | `CapturedTempleOfThreeWindsCombatCatalog`, `CapturedEnemyCombatRuntime`, `OrdinaryEnemyRuntimeService` | `CapturedEnemyCombatProfileCatalogTests`, `CapturedEnemyCombatPacketFactoryTests`, `CapturedEnemyCombatActiveCoverageTests` |
| Named combat | `14/14` PF1931 domains, including successors, owned adds, and Murial | `0` active domains | `CapturedTempleOfThreeWindsEncounterRules`, `CapturedTempleOfThreeWindsEncounterRuntimeService`, `CapturedEnemyCombatRuntimeRegistry` | `DungeonNamedEncounterCompletionTests`, `DungeonNamedLifecycleCompletionTests`, packet factory/catalog tests |
| Nanos | `3` gameplay contracts; `3` explicit active-domain no-nano classifications | `20` actor/family contracts | `CapturedTempleOfThreeWindsEncounterRuntimeService`, `NanoEventRuntimeService`, `ActiveNanoRuntimeService` | `DungeonNamedEncounterCompletionTests`, `DungeonNamedLifecycleCompletionTests` |
| Loot and credits | `21` observed outcome families: `9` ordinary plus `12` named | `2` domains have no proven outcome; selection probabilities/wider pools remain unclaimed for all capture-only tables | `CapturedTempleOfThreeWindsLootDefinitions`, `GlobalLootRuntimeService`, `CorpseInventoryService` | `GlobalLootFoundationTests`, ordinary-content tests, named encounter/lifecycle tests |
| Corpses and atomic inventory | One corpse and one atomic loot result per death; reopen/re-entry never rerolls | No synthetic result for an unproven table | `CorpseInventoryService`, `GlobalLootRuntimeService`, encounter/ordinary death owners | `GlobalLootFoundationTests`, `DungeonNamedLifecycleCompletionTests` |
| Respawn, successors, and adds | `14/14` explicit named classifications plus ordinary policies; Uklesh -> Khalum -> Aztur -> one Uklesh reset | No fallback from named/add domains to ordinary respawn | `DungeonNamedLifecycleCatalog`, `DungeonNamedRespawnScheduler`, `CapturedTempleMainRoomLifecycle`, `WorldRespawnScheduler`, encounter runtime | `DungeonNamedLifecycleCompletionTests` |
| Patrol and movement | `16` captured Cultist patrol slots, Murial's one 20-waypoint patrol, static anchors, chase, leash, and return-home | No invented patrol for static or unsupported actors | `OrdinaryEnemyRuntimeService`, `NpcPatrolReplayCoordinator`, encounter runtime | ordinary-content tests, `NpcChaseNavigationTests`, `OfficialDungeonNavigationTests` |
| Geometry, navigation, and LOS | `30/30` official rooms; every ordinary and named/add anchor grounds; movement and LOS use the official surface/collision graph | Cross-elevation or unreachable routes fail boundedly | `Pf1931OfficialDungeonGeometryLoader`, `OfficialDungeonChaseNavigationProvider`, `NpcChaseNavigationRuntimeService` | `OfficialDungeonNavigationTests`, `PlayfieldCollisionGeometryTests`, `NpcChaseNavigationTests` |
| Doors and world interactions | `43/43` internal doors plus exterior EntryHall statel `C024078B`; official identities only | `0` extra world chest/terminal/portal/room-trigger statels exist | `CapturedPlayfieldDoorStatusRuntimeService`, `TempleDoorProximityRuntime`, `TempleWorldInteractionRules` | `TempleDoorStatusRuntimeTests`, `N3RecoveredContractTests` |
| Entry/exit zoning | PF647 `C0080287` entry and PF1931 `C024078B` exit | No inferred alternate portal | `teleportproxy`, `exitproxyplayfield`, `TeleportMessageHandler`, `PlayfieldAnarchyFMessageHandler`, shared transfer owner | `TempleDoorStatusRuntimeTests`, `N3RecoveredContractTests` |
| Visibility, re-entry, and disposal | Actors, timers, patrols, routes, corpses, loot, door recipient state, nano state, and visibility all have playfield/runtime cleanup | `0` unowned worker classes | `PlayfieldRuntimeSystems`, visibility runtime services, content module, encounter/ordinary owners | `PlayfieldRuntimeOwnershipTests`, named lifecycle, door, nano, and navigation suites |

## Nano disposition inventory

### Gameplay complete (`3`)

| Domain | Nano | Evidence-backed result |
|---|---:|---|
| Reverend Gulard | `205584` | Captured self target and instant nano-data heal. |
| Gartua the Doorkeeper | `205590` | Captured self schedule; exact heal, duration, strain, multi-stat modifiers, refresh, reversal, expiry, death/reset/disposal cleanup. |
| The Re-Animator | `205604` | Captured reanimated-corpse add request and owner lifecycle. |

### Explicit active-domain no-nano (`3`)

Cultist MD26082, Eternal Sentinel MD41690, and Deathless Legionnaire MD42981
have no active-domain nano chain and therefore schedule none.

### Intentionally fail-closed (`20` actor/family contracts)

| Contract group | Count | Exact missing contract |
|---|---:|---|
| Defender `205389/205561/209924` | 3 | Authoritative attack-skill versus Nano Resist resolution; hostile AreaCast membership for `205561`; external source owner/schedule and executable payload for `209924`. |
| Yatila `205600/205594/205592` | 3 | Authoritative resist resolution; `205592` also lacks proven RestrictAction behavior. |
| Re-Animator `205592` | 1 | Proven schedule and RestrictAction behavior, plus resist resolution. |
| Betany `205383` | 1 | Authoritative resist resolution. |
| Curator `205565/205556` | 1 | Hostile AreaCast recipients, authoritative resist resolution, and generic stun/action-lock semantics. |
| Nematet `205395/205378`, `205563/205555`, `205592` | 3 | Authoritative resist resolution, hostile AreaCast recipients where applicable, and generic stun/RestrictAction semantics. |
| Uklesh `204830` | 1 | Proven landed-hit proc probability and generic stun/resist ownership. |
| Murial `70294` | 1 | Categorical missing-buff ally/self selector and safe cadence. The 16 modifiers, duration, strain, and reversal are known but are not enough to schedule it. |
| Cultist MD26074 | 1 | No exact-name Cultist cast; shared MonsterData observations belong to incompatible named actors. |
| Cultist MD26103 family | 1 | Generation/level selector, chain order, cadence, and hostile AreaCast recipients. |
| Cultist MD26135 family | 1 | Target, schedule, and refresh policy from only one observation. |
| Cultist MD26137 family | 1 | Observed family belongs to Caska the Faithful, not an exact-name Cultist. |
| Cultist MD26147 family | 1 | Generation selector and resist resolution for hostile damage. |
| Cultist MD26149 `205580` | 1 | Ally selector and cadence. |

The corpus proves `FinishNanoCasting.Parameter1=1` landed and `3` resisted;
resisted casts apply no child effect. It does not contain the server-side
formula that chooses between those results. No fail-closed row creates a nano
timer, cast packet, modifier, action lock, periodic worker, or active-nano
entry.

## Loot, credits, and corpse disposition

- Complete ordinary outcome families (`9`): Cultist MD26074, MD26082,
  MD26103, MD26135, MD26137, MD26147, MD26149, Eternal Sentinel, and
  Deathless Legionnaire. Exact observed items, quantities, QLs, credits, and
  empty first opens are preserved.
- Complete named outcome families (`12`): Defender, Yatila, Gulard,
  Re-Animator, Betany, Curator, Nematet, Guardian, Gartua, Uklesh, Khalum, and
  Aztur. Each observed corpse inventory is an indivisible atomic snapshot.
- Fail-closed outcome domains (`2`): Reanimated Corpse adds and Murial have no
  proven item, credit, or confirmed-empty corpse outcome. Production generates
  no loot for them and does not claim that the official table is empty.
- Probability boundary: sparse observations do not prove snapshot weights,
  wider pools, or drop chances. `Weight=0`, `DropChanceBasisPoints=0`, and
  `Unresolved` evidence prevent those unknowns from becoming guarantees.
- Lifecycle boundary: one death owns at most one corpse and one atomic result;
  reopen, re-entry, reset, and runtime replacement cannot reroll it.

## Explicit project-policy boundaries

These are implemented and tested private-project policies, not claims of exact
captured timing:

- ordinary respawn uses the shared ordinary policy unless an explicit profile
  overrides it;
- Temple initial named respawn and the post-Aztur full-chain reset use the
  explicit 600-second Temple named policy; and
- Murial uses the explicit 300-second post-despawn ordinary policy.

The corpus contains no complete interval that could replace those policies
with an official timing claim.

## Provenance documents

The following documents are evidence sources, not competing status reports:

- `FINAL_ORDINARY_DUNGEON_COMBAT_COMPLETION_20260728.md`
- `DUNGEON_NAMED_ENCOUNTER_COMPLETION_20260728.md`
- `DUNGEON_NAMED_LIFECYCLE_COMPLETION_20260729.md`
- `DUNGEON_NANO_LOOT_CONTRACT_COMPLETION_20260730.md`
- `PF1931_TEMPLE_NANO_GAMEPLAY_COMPLETION_20260730.md`
- `PF1931_TEMPLE_EXISTING_CORPUS_CONTINUATION_20260731.md`
- `PF1931_TEMPLE_DYNAMIC_DOORS_20260731.md`
- `PF1931_TEMPLE_WORLD_INTERACTIONS_20260731.md`
- the dated room/capture evidence documents under `docs/evidence`.

## Closure verdict

PF1931 Temple of Three Winds is complete for the existing evidence corpus.
Every recoverable population, combat, lifecycle, movement, geometry, door,
zoning, corpse, loot-outcome, and supported nano contract has a production
owner. The exact nano selectors/mechanics and loot probability contracts listed
above remain intentionally fail-closed because neither the complete corpus nor
official client/item/nano resources define them. They are evidence boundaries,
not unfinished implementable work.
