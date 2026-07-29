# PF127/PF1931 Named Dungeon Encounter Completion

Date: 2026-07-28

Starting repository: `master` at
`aa7082a0ede6c98410d7bb089a37ad5abef12715`, synchronized `0/0` with
`origin/master`. The ordinary dungeon population is a locked independent
baseline: PF127 `322/322`, PF1931 `167/167`, combined `489/489`.

## Authoritative runtime inventory

Initial stages, successors, and owned adds are counted once. Murial remains an
ordinary-population actor and is included here only because his named patrol
and combat behavior cross the encounter boundary.

| PF | Runtime owner/profile | Actor or stage | MonsterData | Classification | Result |
| ---: | --- | --- | ---: | --- | --- |
| 127 | `subway.127.boss.abmouth-supremus` | Abmouth Supremus | 155962 | initial boss | complete |
| 127 | `subway.127.boss.vergil-aeneid` | Vergil Aeneid | 203748 | initial boss | complete |
| 127 | `subway.127.named.eumenides` | Eumenides | 203726 | initial named | complete |
| 127 | `subway.127.encounter.abmouth-infector` | Infector slots 0 and 1 | 31909 | owned temporary adds | complete |
| 1931 | `totw.647.boss.defender-of-the-three` | Defender of the Three | 38394 | initial boss | complete |
| 1931 | `totw.647.named.windcaller-yatila` | Windcaller Yatila | 26151 | initial named | complete with bounded packet-only nano behavior |
| 1931 | `totw.647.named.reverend-gulard` | Reverend Gulard | 26147 | initial named | complete with bounded packet-only nano behavior |
| 1931 | `totw.647.boss.the-re-animator` | The Re-Animator | 26155 | initial boss/add owner | complete |
| 1931 | `totw.647.encounter.re-animator.reanimated-corpse` | two Reanimated Corpse slots | 41690 | owned temporary adds | complete |
| 1931 | `totw.647.named.acolyte-betany` | Acolyte Betany | 26143 | initial named | complete with bounded packet-only nano behavior |
| 1931 | `totw.647.boss.the-curator` | The Curator | 22802 | initial boss | complete with bounded packet-only nano behavior |
| 1931 | `totw.647.boss.nematet-the-custodian-of-time` | Nematet the Custodian of Time | 26159 | initial boss | complete with bounded packet-only nano behavior |
| 1931 | `totw.1931.boss.guardian-of-tomorrow` | Guardian of Tomorrow | 22798 | initial boss | complete |
| 1931 | `totw.1931.boss.gartua-the-doorkeeper` | Gartua the Doorkeeper | 159085 | initial boss | complete with bounded packet-only nano behavior |
| 1931 | `totw.1931.boss.uklesh-the-frozen` | Uklesh the Frozen | 40515 | initial chain stage | complete |
| 1931 | `totw.1931.boss.khalum` | Khalum | 95352 | successor stage | complete |
| 1931 | `totw.1931.boss.aztur-the-immortal` | Aztur the Immortal | 159966 | successor stage | complete; full-chain post-Aztur reset remains evidence-bounded |
| 1931 | `totw.ordinary.main-room.murial-the-faithful.26090` | Murial the Faithful | 26090 | ordinary-owned named patrol | complete; ordinary respawn policy retained |

This is 18 unique combat/profile domains: 13 initial stages, two successors,
two owned-add domains, and one ordinary-owned patrol domain. The runtime
initial population contains three PF127 named actors and ten PF1931 initial
named actors plus the two initially materialized Reanimated Corpse add slots.
Successor stages are not double-counted as initial actors.

Strike Foreman is not an active runtime encounter and therefore is not part of
the table above. Existing capture evidence proves appearance, a QL19 weapon,
attack initiation, two normal 18-point other-player results, one 40-point
critical, chase start, corpse visual, credits, and two positive atomic loot
outcomes. Exact respawn, leash/reset, acquisition upper bound, loot-bearing
lifetime, and QL17/QL19 generation selection remain absent. It remains
inactive rather than receiving invented encounter policy.

## Combat and packet domains

All active domains use the shared captured-combat coordinator and packet
factories. Exact categorical ownership remains with the capture contracts:
weapon or natural mode, template family, slot and instance/tag, stream count
and ordering, SpecialAttackWeapon shape, Attack action, hit and damage wires,
normal/miss/critical/terminal class, nano identity, and packet order.
Production continues to own mutable energy/ammunition, actor state, health,
item-derived damage/range/cadence, and health-driven lethal state where the
contract explicitly delegates those values.

PF127 keeps Abmouth's three-stream XOPZ/DENW sequence and captured warp nano
`286237`; Vergil keeps QL23 `122123`, its captured normal/critical semantics,
and level-bounded nanos `43827` and `43880`; Eumenides keeps its exact
owner-linked `123267/123268` weapon domain. Abmouth's two Infector slots use
the captured MD31909 add contract and cannot survive their owner.

PF1931 keeps each profile in
`CapturedTempleOfThreeWindsCombatCatalog` separate. Uklesh, Khalum, and Aztur
retain distinct packet streams and cannot cross-resolve. Reanimated Corpse and
Murial remain separate from Eternal Sentinel despite overlapping ordinary
content ownership. Terminal observations are not promoted to repeating
streams, and player- or pet-owned outcomes are not attributed to an NPC.

## Decisive capture sessions

| Encounter | Capture sessions |
| --- | --- |
| Abmouth | `20260712-224840`, `20260712-232137`, `20260720-053802`, `20260716-220400` |
| Vergil | `20260712-232711`, `20260712-234401`, `20260716-034433`, `20260720-053542` |
| Eumenides | `20260716-034559`, `20260716-222007`, `20260717-214612`, `20260717-214751`, `20260717-215250`, `20260717-220340` |
| Defender | `20260721-035526`, `20260721-040249`, `20260721-040324` |
| Windcaller Yatila | `20260721-041439` |
| Reverend Gulard | `20260721-042139` |
| The Re-Animator / Reanimated Corpse | `20260721-042705`, `20260721-043204` |
| Acolyte Betany | `20260721-044256`, `20260721-052115` |
| The Curator | `20260721-052115`, `20260721-225404` |
| Nematet | `20260721-052115`, `20260721-225743` |
| Guardian of Tomorrow | `20260721-230426`, `20260722-045114` |
| Gartua | `20260721-230824`, `20260722-045421` |
| Murial | `20260721-232051`, `20260721-234614` |
| Uklesh / Khalum / Aztur | `20260721-231151`, `20260722-045552`, `20260722-045835` |

The generated inventory retains the decisive packet identifiers and ordinals
for WIFU, SpecialAttackWeapon, Attack, AttackInfo, MissedAttackInfo, StopFight,
death, and despawn observations. Raw packet bytes remain authoritative.

## Phase, successor, add, and patrol lifecycle

```text
Uklesh active
  -> death cleanup
  -> 0.6822027 s successor timer
  -> Khalum active
  -> death cleanup
  -> 0.211 s successor timer
  -> Aztur active
```

Only one stage may be active. Predecessor state is cleaned before successor
visibility, successor stages suppress independent respawn, and pending
successor work is canceled when the owning runtime is disposed. No
out-of-order or duplicate stage is materialized. A complete captured
post-Aztur abandonment/reset/respawn condition is not present, so production
does not invent an independent chain reset.

The Re-Animator owns exactly two Reanimated Corpse slots. Nano `205604`
requests refill of an empty/dead slot; generation tokens prevent duplicate
materialization. Owner death, reset, and runtime disposal detach living adds,
clear slot state, and prevent orphan combat or respawn.

Abmouth likewise owns two Infector slots. Captured creation/refill timing and
owner cleanup remain encounter-local. Adds are detached from pet ownership
before removal and cannot tick after encounter retirement.

Murial's 20-waypoint patrol is owned by the ordinary runtime. Combat interrupts
the shared movement worker; target loss returns control to the same patrol
state. Runtime replacement cancels the prior worker, so re-entry cannot create
a second patrol loop.

## Nanos and special effects

Exact nano identities, caster/target direction, cast/finish packet shape, and
captured timing are retained. Abmouth warp and Vergil's two level-bounded heals
have proven gameplay ownership. The Re-Animator's `205604` owns add refill.
Other captured Temple named nanos and Gartua's `205590` remain exact
packet-presentation behavior where their downstream stat, duration, stacking,
or removal semantics are not independently proven. No unproven stat mutation
was added and no captured nano identity was silently replaced.

## Corpse, loot, reset, and respawn

Existing exact atomic corpse inventories and credit outcomes remain
encounter-owned and one-time. No probability was inferred from a positive-only
sample. Subway bosses retain ten-minute death-based respawn and 30-minute
loot-bearing corpses. Guardian retains a 1,800-second unlooted corpse; Gartua
retains a 120-second corpse. Other Temple named actors retain the existing
600-second post-NPC-despawn policy only where production already records that
policy. Successor stages do not schedule independent chain respawns. Unknown
pool probabilities, unobserved loot, and the post-Aztur complete-chain reset
remain explicitly unresolved.

## Runtime ownership and re-entry

The root ownership defect was a global encounter registry that stored only the
definition and removed registrations through PF127-specific profile-prefix
logic. PF1931 registrations could therefore survive retirement unless each
identity happened to be remembered by the Temple service.

The registry now stores `(playfield instance, definition)`. Both encounter
services register with their owning playfield, lookups unwrap the definition,
and disposal removes every registration by exact playfield owner. PF127 and
PF1931 remain independent. Temple retirement resets encounter state and add
slots after removing its registry entries. Existing NPC runtime disposal also
stops follow/movement, sets `DoNotDoTimers`, clears combat, corpse, population,
visibility, encounter, add, successor, and respawn ownership.

The deterministic ownership harness proves one first initialization, zero
population materializations on re-entry, reuse of the live encounter
controllers, zero duplicate workers, and full cancellation on replacement.
The historical server trace measured the first PF1931 population construction
at `2.9837 s`; the reused-runtime path performs no second population build.
This task does not claim a live-client portal-to-movement measurement because
the AO client was not launched.

## Full-corpus reproducibility

The aggregate generator stalled because `build_inventory()` retained every
decoded packet from all sessions in one `all_records` collection. Three prior
workers reached approximately 1.59 GB before native failure.

Generation is now bounded to two passes:

1. read per-session metadata/errors and discard decoded records;
2. establish canonical corpus metadata;
3. correlate one capture at a time;
4. retain only records referenced by combat observations plus StopFight and
   despawn boundaries;
5. release session state before processing the next capture.

Stable ordering and semantic identity generation are unchanged. A full write
run completed in `312.3 s`; a second full check completed in `341.4 s` with no
diff. Final counts are 374 sessions, 358 canonical sessions, 2,827 complete
chains, 255 capture-certified profiles, 95 runtime-ready profiles, 303
semantic definitions, 100 runtime-ready definitions, 1,404 unresolved
profiles, and zero generator errors. Formula generation reports 422 profiles
and 67 active bindings. Active coverage reports 1,512 actors, 312 certified
and 1,200 unresolved across the complete multi-playfield coverage surface;
the dungeon ordinary subset remains 489/489.

## Catalog and validation repair

The environment has no Visual Studio `vstest.console.exe` or
`MSBuild.exe`. The approved wrapper now falls back to a deterministic direct
MSTest-compatible runner built with the installed .NET MSBuild and Framework
C# compiler. It executes normal test classes and lifecycle methods without
reflection-time `TestContext` injection. Diagnostic writes are null-safe.

The Molested Molecules ownership assertion exposed a real contract propagation
gap: `WithProductionEquippedWeaponValues()` did not mark damage and timing as
production-owned, and the raw-profile enrichment path dropped that ownership.
Both flags now propagate while the exact weapon and packet semantics remain
unchanged. The focused Molested regression and the complete 51-test combat
catalog sweep pass.

The consolidated named suite passes all six tests and proves the 18-domain
inventory, unique stage classification, exact ready combat domains,
successor/add ownership, playfield-owned retirement, disposal cancellation,
and the locked 489/489 ordinary baseline. The combined focused Abmouth, Temple,
runtime-ownership, and named suites pass 45/45. Active-coverage validation
passes 3/3, including direct source-local capture-certified contracts such as
Murial when no canonical aggregate profile exists.

The full direct messaging run completed at 642 pass / 50 fail. The failures
remain separable from this task: missing local fixture/content files,
pre-existing source-ownership guardrails affected by preserved mission work,
and older population/visibility assertions. Focused task suites, full catalog,
bounded generation, deterministic no-diff generation, formula/coverage
generation, and production compilation are the named-encounter acceptance
gates.

## Final completion matrix

- Active runtime combat/profile domains: 18/18 complete.
- Initial named encounter profiles: 13/13 complete.
- Successor profiles: 2/2 complete.
- Owned-add domains: 2/2 complete.
- Ordinary-owned named patrol domains: 1/1 complete.
- Ordinary actors: 489/489 unchanged.
- Bounded packet-only nano behavior: implemented without invented effects.
- Exact unresolved boundaries: post-Aztur full-chain reset/respawn,
  downstream effects for presentation-only Temple nanos, unknown loot
  probabilities, Murial-specific respawn/nano effect, and inactive Strike
  Foreman generation/lifecycle policy.
