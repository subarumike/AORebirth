# Temple of Three Winds authoritative full-corpus acceptance matrix - 2026-08-01

## Acceptance decision

**PF1931 Temple of Three Winds is complete for every behavior supported by the
complete existing repository and capture corpus.** This file is the sole
current Temple status authority. Earlier Temple room reports, quarantine notes,
nano/loot reports, interaction reports, and
`PF1931_TEMPLE_ACCEPTANCE_MATRIX_20260801.md` remain evidence and provenance;
where their status, blocker, denominator, or test count differs, this matrix
wins.

Acceptance requires a production-reachable owner, exact evidence boundary, and
focused regression. Unsupported selectors, probabilities, and hidden client or
server rules are non-blocking evidence gaps. They are not populated with
defaults and do not suppress independently supported behavior.

## Complete corpus searched

The canonical combat extractor was run again over the complete existing raw
corpus, not only the checked-in projection. The isolated run reported:

| Measure | Result |
|---|---:|
| Capture sessions discovered | `381` |
| Canonical-valid sessions | `365` |
| Complete combat chains | `3,269` |
| Capture-certified profiles | `260` |
| Runtime-ready generated profiles | `96` |
| Capture-certified semantic definitions | `309` |
| Runtime-ready semantic definitions | `101` |
| Decode or projection errors | `0` |

The Temple interaction audit additionally includes all `36` PF1931-associated
sessions: `32` current-realm sessions, PF647 boundary session
`20260722-041602`, and legacy raw-hex sessions `20260528-190456`,
`20260528-191120`, and `20260528-192819`. Current sessions were searched through
`raw-packets.csv` and the three legacy sessions through their complete
`packets.hex.log` sinks.

The audit covered raw packets and all available SCFU, movement, enemy combat,
state, lifecycle, respawn, corpse, loot, inventory, vendor, shop, interaction,
chat, quest/mission, teleport, door, and playfield projections. It also covered:

- `docs/generated/capture_backed_npc_combat_inventory.json` and the regenerated
  active-coverage projection;
- the combat setup/formula, secondary-evidence, movement, loot, lifecycle, and
  population datasets and exact-byte fixtures;
- every dated Temple evidence report under `docs/evidence`;
- official client `playfields.dat`, RDB playfield resource `1931`, tilemap
  resource `1930`, `items.dat`, and client `18.8.62_EP1` nano data;
- the mapped statel, teleport, door, nano, combat, modifier, and geometry/client
  routines; and
- all production owners and focused tests named below.

No new capture was requested or run.

## Canonical promotion correction

The shared active-combat coverage generator contained an artificial omission:
it counted only `153` of the `167` ordinary Temple actors, omitted all `14`
Deathless Legionnaires, and stopped the named surface before Uklesh, Khalum,
and Aztur. It also applied the generic raw-profile readiness gate to exact
Temple encounter contracts already owned by production and exact packet tests.

The canonical projection now parses all `167` ordinary actors, all `12` named
stages, and both Reanimated Corpse slots. The exact PF1931 owner is recognized
only for the Deathless capture-proven equipped archetype and the explicit
Temple encounter/add contracts. It cannot enable cross-playfield fallback,
automatic aggro, automatic activation, or captured runtime identity mapping.

| Active coverage surface | Actors | Certified | Unresolved |
|---|---:|---:|---:|
| `temple-ordinary` | `167` | `167` | `0` |
| `temple-named-encounters` | `12` | `12` | `0` |
| `temple-reanimated-corpse-adds` | `2` | `2` | `0` |
| **PF1931 total** | **181** | **181** | **0** |

Captured identities remain evidence provenance only. Runtime actors continue to
receive server-owned identities.

## Authoritative acceptance matrix

| Capability | Accepted evidence boundary | Production owner | Focused tests | Acceptance |
|---|---|---|---|---|
| Population and appearance | Exact `167` ordinary slots, `12` named stages, and two owned add slots; exact source, level, MonsterData, appearance, position, and heading definitions. | `TempleOfThreeWindsContentModule`, `CapturedTempleOfThreeWindsContentProvider`, `CapturedTempleOfThreeWindsEncounterRuntimeService`, `OrdinaryEnemyCatalog` | `TempleOfThreeWindsOrdinaryContentTests`, `DungeonNamedEncounterCompletionTests`, PF1931 active-coverage test | **Accepted - exact** |
| Movement and patrol | Sixteen Cultist patrol slots, five Deathless patrols, Murial's exact 20-waypoint patrol, static anchors, chase, return-home, leash, and bounded interruption. | `OrdinaryEnemyRuntimeService`, `NpcPatrolReplayCoordinator`, encounter runtime, shared chase runtime | ordinary content, `NpcChaseNavigationTests`, `OfficialDungeonNavigationTests` | **Accepted - exact routes; policy thresholds labeled** |
| Aggro | Capture-proven proactive/retaliation behavior remains actor scoped; explicit private radii are policy, not official measurements. No combat-contract promotion silently enables aggro. | ordinary aggression profiles, encounter rules/runtime | ordinary and named completion tests; PF1931 coverage guard | **Accepted - proven direction plus labeled policy** |
| Ordinary combat | Every `167` ordinary actor resolves a production-ready exact or formula/archetype-compatible contract. Deathless L48-L50 uses the capture-proven equipped archetype with production item-derived values. | `CapturedTempleOfThreeWindsCombatCatalog`, `CapturedEnemyCombatProfileCatalog`, `CapturedEnemyCombatRuntime`, ordinary runtime | profile catalog, setup generator, packet factory, active coverage | **Accepted - 167/167** |
| Named/add combat | Twelve named stages and two add slots retain captured stream categories, damage, packet order, weapon state, and timing boundaries through explicit shared contracts. | Temple combat catalog and encounter runtime | named completion/lifecycle and exact packet tests | **Accepted - 14/14 domains** |
| Death, corpses, and loot atomicity | One death owns one corpse and one atomic inventory result. Reopen, re-entry, reset, and replacement do not reroll. | encounter/ordinary death owners, `CorpseInventoryService`, `GlobalLootRuntimeService` | ordinary, named lifecycle, and focused global-loot tests | **Accepted - exact lifecycle** |
| Loot and credits | Nine ordinary and twelve named observed outcome families preserve exact items, quantities, QLs, credits, and empty outcomes as indivisible snapshots. | `CapturedTempleOfThreeWindsLootDefinitions`, ordinary loot adapters, global loot runtime | ordinary/named tests and global-loot evidence guards | **Accepted - 21 observed families** |
| Respawn, successors, and adds | Explicit policies for all named domains; Uklesh -> Khalum -> Aztur -> exactly one Uklesh reset; adds cannot outlive Re-Animator; Murial respawns once with one patrol. | `DungeonNamedRespawnScheduler`, `CapturedTempleMainRoomLifecycle`, encounter/ordinary runtime | `DungeonNamedLifecycleCompletionTests` | **Accepted - exact ownership; policy timing labeled** |
| Supported nanos | Gulard `205584`, Gartua `205590`, and Re-Animator `205604` apply and clean through shared nano/active-modifier owners. Explicit no-nano classifications schedule nothing. | encounter runtime, `NanoEventRuntimeService`, `ActiveNanoRuntimeService` | named encounter/lifecycle tests | **Accepted - three gameplay contracts** |
| Geometry, collision, navigation, and LOS | Official 30-room geometry grounds all ordinary/named/add anchors; walls block movement and LOS; valid doors/passages connect required rooms; routes fail boundedly. | `Pf1931OfficialDungeonGeometryLoader`, official navigation provider, chase runtime | collision, chase, and official navigation tests | **Accepted - 30/30 rooms** |
| Doors | All 43 internal official statels use the shared 0.5 m per-recipient proximity state; exterior `C024078B` remains the zoning edge. | door status runtime, `TempleDoorProximityRuntime` | `TempleDoorStatusRuntimeTests`, `N3RecoveredContractTests` | **Accepted - 43/43 internal** |
| Entry/exit zoning | PF647 `C0080287` entry and PF1931 `C024078B` exit retain exact targets, landings, headings, edge-triggering, and cleanup. | shared statel/teleport/playfield transfer owners | door and N3 contract tests | **Accepted - exact supported boundary** |
| Vendors | Complete projections and official PF1931 statels contain no world vendor identity or complete VendorFull + ShopUpdate contract. | shared vendor service remains unbound for PF1931 | Temple acceptance ownership guard | **Accepted absence - no synthetic vendor** |
| Interactions and world objects | Official inventory contains 44 doors and no PF1931 chest, terminal, portal, room-trigger, or other Use statel. GenericCmd rows resolve to doors, corpses, characters, and owned inventory containers. | shared interaction/statel owners | door/N3/acceptance tests | **Accepted - complete official inventory** |
| Dialogue | Chat projections contain protocol traffic, loot notifications, and combat/system NPC messages, but no complete PF1931 dialogue root/options/replies contract. | no PF1931 dialogue registration | Temple acceptance ownership guard | **Accepted absence - no invented dialogue** |
| Quests and missions | Raw and generated Temple projections contain no complete PF1931 quest creation, objective, reward, or mission-state chain. PF647 entry gating is a statel contract, not a quest. | no PF1931 quest/mission registration | Temple acceptance ownership guard | **Accepted absence - no invented quest/mission** |
| Visibility, re-entry, and disposal | Actors, combat, nanos, timers, patrols, routes, corpses, loot, doors, statel contacts, and visibility are playfield-owned and cleared on replacement/disposal. | `PlayfieldRuntimeSystems` and subsystem owners | runtime ownership, lifecycle, door, navigation tests | **Accepted - zero unowned PF1931 workers** |

## Genuine non-blocking evidence gaps

| Missing contract | Proven boundary retained | Why unsupported |
|---|---|---|
| Attack-skill versus Nano Resist selection, hostile AreaCast recipients, stun/RestrictAction behavior, Uklesh proc probability, Murial ally selector/cadence, and several Cultist/named schedules | Three supported gameplay nanos and three explicit no-nano domains remain active; 20 actor/family nano contracts remain fail-closed. | Packet occurrences and nano records do not define the missing server selector, probability, target set, or action-lock runtime. |
| Murial and Reanimated Corpse loot outcomes | No loot is generated for those two unproven domains; owned corpse lifecycle still applies. | No item, credit, or confirmed-empty first-open outcome exists. |
| Official loot probabilities and unseen wider pools | Every observed atomic snapshot remains selectable without cross-corpse mixing. | Sparse outcomes prove membership and multiplicity, not official weights or exhaustiveness. |
| Exact official ordinary/named/Murial/reset timing where only project policy exists | Explicit 300-second ordinary/Murial and 600-second named/full-chain policies remain labeled and tested. | No complete captured interval replaces those policies. |

These gaps do not block any independently proven behavior and create no hidden
timer, actor, packet, loot result, modifier, or runtime identity mapping.

## Acceptance suite

The deterministic Temple release gate is:

```cmd
tools\run_temple_acceptance_tests.cmd
```

It covers the acceptance owner, the complete `181/181` active PF1931 combat
surface, ordinary/named/add content, packet paths, death/respawn/loot/nanos,
geometry/navigation/LOS, doors, zoning, interaction absence, and runtime
teardown. The approved Debug build and engine restart wrappers remain the final
executable checks.

## Final evidence-discipline confirmation

No valid Temple observation was rejected because it used an older session,
different captured generation, manual exact contract, one-shot stream, missing
closed loop, or incomplete unrelated probability. No runtime behavior was
created from a player inventory container, loot chat message, captured runtime
identity, nearest-level substitution, cross-playfield family default, or
unsupported vendor/dialogue/quest/mission inference.
