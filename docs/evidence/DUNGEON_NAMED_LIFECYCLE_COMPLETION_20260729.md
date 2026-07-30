# Named Dungeon Lifecycle Completion

TASK ID: `DUNGEON-LIFECYCLE-COMPLETION-001`

Date: 2026-07-29

Starting repository SHA: `80d61a09437692cb5359862ae40bdddb459023a7`.
At task start local `master` and `origin/master` matched at that SHA, and
`fec882baec764b7eac86663e8126b94f34daf827` was an ancestor of `HEAD`.

## Result

PF127 and PF1931 now use one shared named-respawn scheduling model with
playfield-scoped cancellation and one schedule per profile. A separate
main-room phase owner makes the Uklesh -> Khalum -> Aztur -> Uklesh state
transition explicit. The shared corpse inventory rejects a second corpse for
the same dead NPC generation before another global loot roll can occur.
Playfield disposal explicitly clears pending corpse creation, pending credit
awards, materialized corpse inventories, encounter registrations, combat,
movement, patrol, respawn, and visibility ownership.

No ordinary-enemy fallback owns any named encounter, successor, or owned add.
Murial remains deliberately owned by the existing ordinary population,
movement, corpse, loot, and world-respawn systems.

## All 19 respawn classifications

| PF | Domain | Kind | Recreation owner and trigger | Delay | Corpse / loot dependency | Player / owner dependency | Final classification |
|---:|---|---|---|---|---|---|---|
| 127 | Abmouth Supremus | initial | PF127 named service; death | 600s after death | independent of corpse and loot lifetime | no player dependency | exact rule already implemented |
| 127 | Vergil Aeneid | initial | PF127 named service; death | 600s after death | independent of corpse and loot lifetime | no player dependency | exact rule already implemented |
| 127 | Eumenides | initial | PF127 named service; death | 600s after death | independent of corpse and loot lifetime | no player dependency | exact rule already implemented |
| 127 | Abmouth-owned Infector adds | owned add | Abmouth slot owner; owner action/refill | initial 1.212281/2.326367s; refill cycle 0.830/0.380/3.322/3.490s | killed adds use shared corpse/loot; neither permits independent recreation | living Abmouth required | explicitly no independent respawn |
| 127 | Strike Foreman | initial | PF127 named service; death | 600s after death | independent of corpse and loot lifetime | no player dependency | exact rule already implemented |
| 1931 | Defender of the Three | initial | Temple named service; NPC despawn | 600s after NPC despawn | independent of corpse and loot lifetime | no player dependency | exact rule already implemented |
| 1931 | Windcaller Yatila | initial | shared Temple named rule; NPC despawn | 600s after NPC despawn | independent of corpse and loot lifetime | no player dependency | proven shared named-respawn rule |
| 1931 | Reverend Gulard | initial | shared Temple named rule; NPC despawn | 600s after NPC despawn | independent of corpse and loot lifetime | no player dependency | proven shared named-respawn rule |
| 1931 | Re-Animator | initial/add owner | shared Temple named rule; NPC despawn | 600s after NPC despawn | independent of corpse and loot lifetime; death retires owned adds | no player dependency | proven shared named-respawn rule |
| 1931 | Reanimated Corpse adds | owned add | Re-Animator action/slot owner | 1.578s after cast; 1.123s after requested add despawn; 1s reset refill | killed adds use shared corpse/loot; neither permits independent recreation | living Re-Animator required | explicitly no independent respawn |
| 1931 | Acolyte Betany | initial | shared Temple named rule; NPC despawn | 600s after NPC despawn | independent of corpse and loot lifetime | no player dependency | proven shared named-respawn rule |
| 1931 | Curator | initial | shared Temple named rule; NPC despawn | 600s after NPC despawn | independent of corpse and loot lifetime | no player dependency | proven shared named-respawn rule |
| 1931 | Nematet | initial | shared Temple named rule; NPC despawn | 600s after NPC despawn | independent of corpse and loot lifetime | no player dependency | proven shared named-respawn rule |
| 1931 | Guardian of Tomorrow | initial | Temple named service; NPC despawn | 600s after NPC despawn | independent of corpse and loot lifetime | no player dependency | exact rule already implemented |
| 1931 | Gartua | initial | Temple named service; NPC despawn | 600s after NPC despawn | independent of corpse and loot lifetime | no player dependency | exact rule already implemented |
| 1931 | Uklesh | initial chain stage | Aztur chain-complete reset; Aztur NPC despawn | 600s shared Temple named policy | predecessor corpses and loot do not block reset | Aztur chain owner required; no player dependency | exact rule implemented in this task |
| 1931 | Khalum | successor | Uklesh death | 0.6822027s | Uklesh corpse and loot do not block successor | predecessor required | explicitly no independent respawn |
| 1931 | Aztur | successor | Khalum death | 0.211s | Khalum corpse and loot do not block successor | predecessor required | explicitly no independent respawn |
| 1931 | Murial | ordinary-owned named patrol | world population; NPC despawn | 300s explicit shared ordinary policy | independent of 30/180/30s corpse state | no player dependency; one ordinary spawn owner | proven shared named-respawn rule |

Every entry has one live-runtime identity path, at most one pending schedule,
live re-entry reuse, and replacement-runtime cleanup. PF127 and PF1931
schedules carry distinct playfield owners; canceling one playfield leaves the
other intact.

## Post-Aztur reset

The exact successor path remains:

```text
Uklesh death
  -> 0.6822027 seconds
  -> exactly one Khalum
Khalum death
  -> 0.211 seconds
  -> exactly one Aztur
Aztur death
  -> dead/chain-complete state
Aztur NPC despawn
  -> exactly one Uklesh reset schedule at +600 seconds
  -> exactly one initial-position Uklesh in clean active state
```

The reset delay is not derived from either successor delay. No reviewed raw
capture contains a complete Aztur-despawn-to-Uklesh-recreation interval, so
there is no authoritative reset packet ordinal. The 600-second delay is owned
by the already established shared Temple named-respawn policy. This remains a
policy-backed result, not a captured timing claim.

The explicit phase owner rejects out-of-order and duplicate stages. A player
re-entering while Khalum or Aztur owns the chain cannot cause activation to
materialize another Uklesh. A live re-entry while the reset is pending reuses
the schedule. Re-entry after reset sees the single active Uklesh. Disposal at
any phase cancels successor/reset work and marks the phase retired; a
replacement runtime starts from one clean initial Uklesh and retains no old
actor or timer.

Khalum and Aztur schedules are canceled before the reset schedule is installed.
Their old runtime registrations are removed by NPC despawn and the
playfield-owned encounter registry cleanup. Corpse and loot state remain
separate and may expire independently.

## Murial lifecycle

Murial keeps source identity `0x7987F12D`, MD26090, L34, the exact original
anchor `(271.4782,14.8112507,445.842255)`, and the existing 20-waypoint patrol.
Death clears combat tracking, chase state, target/fighting target, and the
controller follow worker before corpse handling. The shared world population
owner releases the dead generation at NPC despawn and schedules one replacement
at +300 seconds. Respawn creates one actor at the original anchor with health,
movement, aggression, and waypoint state reset; `ApplyMovement` installs the
same waypoint list once and the source-identity guard prevents a second patrol
worker. Live re-entry reuses that actor. Runtime replacement clears the old
population identity, movement, combat, patrol, respawn, corpse, loot, and
visibility state.

Murial corpse CATMesh `5927` and lifetime policy remain 30 seconds born-empty,
180 seconds unlooted, and 30 seconds after becoming empty. Loot and credits
remain fail-closed because no identity-linked inventory was opened.

## Corpse and loot ownership

| Domain group | Corpse owner | Loot owner | Atomicity and reopen/re-entry | Retirement / respawn interaction |
|---|---|---|---|---|
| Abmouth, Vergil, Eumenides, Strike Foreman | playfield corpse scheduler and `CorpseInventoryService` | `GlobalLootRuntimeService` captured atomic snapshots | one pending corpse per dead NPC; one global roll at registration; reopening and live re-entry reuse `CorpseState` | corpse expiry/loot cleanup is independent of named death-based respawn |
| Defender, Yatila, Gulard, Re-Animator, Betany, Curator, Nematet, Guardian, Gartua, Uklesh, Khalum, Aztur | same shared corpse owner with encounter-specific lifetimes | `CapturedTempleOfThreeWindsLootDefinitions` through global loot | one corpse and one materialized selection per death; no reopen/re-entry reroll | Temple NPC-despawn respawn and chain reset do not wait for corpse or loot |
| Infector and Reanimated Corpse adds | same shared corpse owner when killed | existing global loot definition or fail-closed empty result | one corpse/roll per killed generation; owner cleanup despawn does not fabricate a death corpse | owner action alone recreates slots; corpse/loot never creates an independent add |
| Murial | ordinary profile corpse policy through shared corpse owner | global loot fail-closed because contents are unproven | one corpse state per dead generation; reopen/re-entry reuse it | +300s NPC-despawn respawn is independent of corpse retirement |

`ScheduleCorpseSpawn` now rejects a second pending or materialized corpse for the
same dead NPC before `GlobalLootRuntimeService.Generate` can run. The corpse
inventory independently rejects duplicate corpse identity and duplicate dead
NPC ownership. Materialized items, credits, opened state, and looted flags live
in the corpse state until transfer or expiry. Playfield disposal explicitly
clears pending corpse spawns, pending credit awards, materialized corpse states,
and corpse timers.

No loot item, pool member, probability, or credit outcome was added by this
task.

## Shared worker ownership

| Owned state / worker | Creation path and uniqueness | Cancellation and disposal | Live re-entry / replacement |
|---|---|---|---|
| Encounter registrations | named spawn registers one runtime identity with exact playfield | NPC despawn removes identity; runtime disposal removes all registrations for the playfield | live reuse; replacement begins empty |
| Active named actors | service state or ordinary population source map | death/despawn clears identity; disposal retires timers and actors | activation checks identity and schedule before spawning |
| Successor/reset timers | one `DungeonNamedRespawnScheduler` per dungeon runtime, backed by `WorldRespawnScheduler` | profile uniqueness plus playfield-scoped cancel | live due time retained; replacement retains none |
| Owned-add timers | Abmouth/Re-Animator slot arrays with generation state | owner death/reset/disposal clears due work and detaches living adds | live slots reused; replacement slots start empty |
| Combat schedules | shared NPC combat tick owner | death and disposal clear tracking and set retired NPC timers off | no retired tick can execute |
| Movement/chase workers | shared NPC movement/chase owner | death clears chase/follow; disposal clears all navigation | live actor resumes its state; replacement owns new state |
| Murial patrol | ordinary actor waypoint/controller owner | death stops follow; disposal clears population and movement | one source identity guard and one `ApplyMovement` path |
| Corpse timers and loot | corpse scheduler plus per-playfield inventory | loot/expiry removes state; disposal clears pending/materialized state | live reopen/re-entry reuse; replacement retains none |
| Respawn timers | shared named scheduler or ordinary world scheduler | playfield cancellation on disposal | PF127/PF1931 isolated |
| Visibility | playfield visibility interest owner | NPC/corpse removal and runtime clear unregister | replacement registers only new identities |

## Raw capture sessions and decisive ordinals

The existing finalized corpus remains authoritative; this task did not launch
the AO client or create a new capture.

| Evidence | Session and decisive ordinal/result |
|---|---|
| Main-room chain identity and combat | `20260721-231151`, `20260722-045552`, `20260722-045835`; Uklesh nano identity `204830` first decisive ordinal 1795 in `20260721-231151`; successor delays remain the exact correlated death/spawn intervals 0.6822027s and 0.211s |
| Post-Aztur reset | no session contains the full Aztur-despawn-to-new-Uklesh interval; therefore no reset ordinal or capture-timed delay exists |
| Re-Animator add ownership | `20260721-043204`, nano `205604` decisive ordinal 3865 correlated with one missing Reanimated Corpse slot |
| Murial combat/corpse | `20260721-232051`; nano `70294` ordinal 16416 is self-targeted, but selector/effect remain unresolved; five exact 26-point normal hits and the corpse boundary are identity-linked |
| Murial patrol | `20260721-234614`; 42 identity-linked `FollowTarget/NpcPath` rows prove the 20-destination loop twice and begin a third loop |
| Defender/Yatila/Gulard/Betany/Curator/Nematet/Gartua lifecycle-adjacent packet evidence | `20260721-033006` ordinals 2234/2260/2265/2365; `20260721-041439` ordinals 2323/2501/4833; `20260721-042139` ordinal 1337; `20260721-044256` ordinal 568; `20260721-225404` ordinals 1653/1721 and 1778/1838; `20260721-225743` ordinals 1715, 1785/1818, 1859/1895, 2173/2201; `20260721-230824` ordinal 769 |

These ordinals prove packet identity, order, and the documented bounded
effects. They do not manufacture missing respawn intervals or loot
probabilities.

## Deterministic acceptance

The new consolidated `DungeonNamedLifecycleCompletionTests` suite contains 20
tests matching the requested lifecycle acceptance list. It proves the 19-entry
catalog, complete main-room transition, schedule uniqueness/cancellation,
owned-add dependency, Murial death/respawn/patrol ownership, corpse and loot
atomicity, replacement cleanup, playfield isolation, Strike Foreman
regression, the 489 ordinary catalog, 19 named domains, and mission-domain
separation.

Runtime acceptance remains deterministic server-side evidence plus build and
engine validation. No AO client was launched.

Validation on the completed implementation:

- `DungeonNamedLifecycleCompletionTests`: **20/20 PASS**.
- `DungeonNamedEncounterCompletionTests`: **10/10 PASS**.
- `AbmouthEncounterRuntimeServiceTests`: **27/27 PASS**.
- `GlobalLootFoundationTests`: **10/10 PASS**.
- `TempleOfThreeWindsOrdinaryContentTests`: **7/7 PASS**.
- `CapturedEnemyCombatPacketFactoryTests`: **38/38 PASS**.
- `CapturedEnemyCombatProfileCatalogTests`: **51/51 PASS**.
- `PlayfieldCollisionGeometryTests`: **17/17 PASS**.
- `NpcChaseNavigationTests`: **38/38 PASS**.
- `WorldPopulationFoundationTests`: **35/39 PASS**; its four existing
  ordinary-combat/source-resolution failures are outside this lifecycle diff.
- `CapturedEnemyCombatActiveCoverageTests` accepted both task-owned content
  hashes, then stopped on a concurrently modified mission content input.
- Complete direct suite: **731/773 PASS** in the shared dirty worktree; all 42
  reported failures are preserved non-task baselines or concurrent mission,
  inventory, dialogue, Arete, combat-source, and generated-input work.
- Approved Debug build: **PASS** after the engines were stopped to release
  output locks.
- `git diff --check`: **PASS**.
- `git lfs fsck`: **PASS**.
- `git lfs status`: no LFS objects pending.

## Exact unresolved boundaries

1. No raw capture proves the post-Aztur reset interval. The 600-second reset is
   the shared Temple named policy, not a capture-timed Aztur rule.
2. No capture proves a player-presence modifier for named respawn. The shared
   runtime schedules independently of occupants while alive and cancels on
   disposal.
3. Murial loot/credits and nano `70294` target selection, cadence, effect,
   stacking, and expiry remain unproven and fail-closed.
4. Exact official-live Murial patrol timing reproduction remains unproven; the
   path is exact, while the private runtime uses the shared waypoint worker.
5. Wider named/add loot pools and selection probabilities remain unresolved
   where existing evidence contains only positive or incomplete snapshots.
6. Packet-only Temple nano downstream gameplay remains outside this lifecycle
   task.
7. Live-client re-entry and post-reset observation were not performed because
   the AO client was not launched.
