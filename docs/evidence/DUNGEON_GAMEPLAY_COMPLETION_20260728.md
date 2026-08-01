# PF127/PF1931 Dungeon Gameplay Completion

> **PF1931 status authority (2026-08-01):** Historical PF1931 evidence/provenance only. Later nano, geometry, door, zoning, and lifecycle passes superseded its PF1931 status claims. Current PF1931 status is the [Temple acceptance matrix](PF1931_TEMPLE_ACCEPTANCE_MATRIX_20260801.md).

Date: 2026-07-28

## Scope and baseline

The accepted combat baseline is commit
`9cbd35d94f98af33f89a8fc21dd69847cfbfa8b2`. Concurrent mission-spatial
commit `5304a09bbb1e702e3811954d6cb52cf50b8d6b7e` was already synchronized
before this gameplay slice resumed and remains preserved.

This pass does not change the locked combat totals:

- PF127/PF1931 ordinary combat: `489/489`.
- Active named combat/profile domains: `19/19`.
- Strike Foreman is active as a PF127 named encounter.

The work below addresses lifecycle, reset, nano-effect ownership, respawn,
corpse, and loot contracts without inventing missing categorical behavior or
probabilities.

## Completed unresolved-contract matrix

| Area | Starting gap | Current code owner, evidence, and missing fields | Final disposition |
|---|---|---|---|
| Strike Foreman | QL17/QL19 selection disconnected the captured actor from runtime | `CapturedSubwayEncounterRuntimeService` owns the active L19 spawn and shared named lifecycle. `CapturedSubwayCombatCatalog` owns exact `122767/122768`, slot-6, WIFU/SAW/Attack/AttackInfo semantics. Production selects QL19 from actor level inside the template range and owns damage, range, cadence, Energy, and mutable state. | Implemented. The two capture-proven corpse snapshots retain atomic membership, while item QL follows enemy level inside each valid template range. Wider pool membership and probabilities remain unresolved. |
| Uklesh -> Khalum -> Aztur reset | No full-chain recreation after Aztur | `CapturedTempleOfThreeWindsEncounterRuntimeService` owns identities and scheduling; `CapturedTempleOfThreeWindsEncounterRules` owns the categorical reset rule. The independently authoritative Temple named policy supplies 600 seconds. No capture supplies an Aztur-to-Uklesh interval. | Implemented. Aztur NPC despawn schedules exactly one Uklesh at `+600s`; living or already scheduled stages reject duplicates, dead corpses remain independent, and runtime disposal clears the schedule. |
| Temple nanos | Packet identities existed, but effect ownership was not explicit and several observed IDs were unscheduled | `CapturedTempleOfThreeWindsEncounterRuntimeService` owns scheduled named casts; `OrdinaryEnemyRuntimeService` owns fully specified ordinary support nanos; `NanoEventRuntimeService` cannot select hostile/ally targets. Missing per unscheduled nano: categorical target and/or cadence and downstream stat/damage/heal/duration/stack/removal contract. | Exact emitted packet behavior is retained. Only `205604` owns proven gameplay: a Reanimated Corpse add request. Other named IDs are packet-only; identities without a complete schedule remain unscheduled. |
| Murial | Generic respawn assignment and unresolved nano | `CapturedTempleOfThreeWindsContentProvider` and `WorldRespawnScheduler` own respawn/patrol; sessions `20260721-232051/234614` own anchor, combat, corpse, and 20 waypoints. Missing: nano `70294` target selector/cadence/effect and loot/credits. | Implemented explicit Murial policy: `300s` after NPC despawn, original anchor, full health/movement/aggression reset, and one population-owned patrol. Nano and loot remain fail-closed. |
| Named respawns | Successor suppression was a boolean and ordinary/default ownership was not explicit | PF127 `CapturedSubwayEncounterRuntimeService`, PF1931 `CapturedTempleOfThreeWindsEncounterRuntimeService`, and ordinary `WorldRespawnScheduler` own the 19 domains. Exact and policy provenance is recorded below; no domain remains ownerless. | All 19 domains classify explicitly as captured independent, policy independent, successor-only, owner-only, or chain-reset. Successors and adds cannot independently respawn. |
| Loot/corpse | Eumenides empty cleanup did not use the captured boundary; selection probabilities remain unknown | `GlobalLootRuntimeService`, `CapturedTempleOfThreeWindsLootDefinitions`, `CorpseInventoryService`, and corpse lifecycle own atomic snapshots and retirement. Missing: official probabilities/wider pools for incomplete domains, plus Murial loot. | Eumenides retains a 30-minute loot-bearing corpse and now retires within the shared three-second empty bound. Existing atomic outcomes remain atomic. No unresolved probability became guaranteed or received invented weights. |

## Strike Foreman

### Proven contract

Strike Foreman is MonsterData `203744`, level `19`.

- Six local-player normal results are exactly `13`, with two misses.
- SpecialAttackWeapon is exactly `154/154/154/117/0`.
- AttackInfo uses slot `6`, unknown `0`, instance `0`.
- Same-source landed intervals are `5.3870402..5.7927872s`, median
  `5.4165278s`.
- Capture `20260709-222339` proves proactive acquisition at a
  `20.250672` horizontal-unit lower bound and chase behavior.
- Corpse CATMesh is `17870`; corpse credits are `176`.
- `20260720-032106` proves one atomic three-item outcome:
  `27199` QL10, `123744/123745` QL20, and `301713` QL1.
- `20260720-033513` proves a separate atomic two-item outcome:
  `85676/22072` QL15 and `301707` QL1.

### Active production ownership

The active level-19 spawn selects QL19 from template family `122767/122768`
through the shared enemy-level/item-range rule. Capture remains authoritative
for equipped mode, slot `6`, WIFU structure, `154/154/154/117/0` SAW,
Attack action `0`, AttackInfo hit/damage wires `3/0`, and instance `0`.
Production owns QL, damage, range, cadence, Energy, ammunition, and mutable
weapon state.

`CapturedSubwayEncounterRuntimeService` owns the captured spawn, conservative
capture-proven automatic acquisition through `20.250672` units, the shared
100-unit leash, regular 60-second loot-bearing corpse, shared PF127 named
600-second respawn, and runtime disposal. The observed acquisition distance is
not claimed as a proven upper bound; behavior beyond it remains fail-closed.
The actor remains outside `CapturedSubwayOrdinaryContentProvider` and the
locked `489/489` ordinary denominator.

The QL19 WIFU is exact from `20260709-220439` packet `6672`. The exact
SAW/Attack/13-point AttackInfo suffix is from `20260720-033513` packets
`38/39/72`. These are correlated categorical observations across active
generations, not mislabeled as one capture-local chain.

The two captured loot snapshots remain exact atomic memberships. Item quality
follows enemy level within each template's valid QL range; exact
loot-outcome probabilities and wider pool membership remain unresolved.

## Post-Aztur full-chain reset

The existing progression remains:

```text
Uklesh death
  -> 0.6822027s
Khalum
  -> 0.211s after Khalum death
Aztur
  -> corpse/NPC lifecycle completes
Aztur NPC despawn
  -> 600s Temple named policy
Uklesh
```

The reset begins on the existing `NotifyNpcDespawn` boundary, not on the
captured successor delays, damage, death packet alone, loot transfer, or room
vacancy. That boundary is reached after the current corpse owner retires the
dead NPC. Loot and corpse state therefore remain owned by corpse lifecycle and
are not deleted by the reset scheduler.

`CapturedTempleOfThreeWindsEncounterRules.TryResolveMainRoomResetDue` rejects
the reset if Uklesh, Khalum, or Aztur is living or already scheduled. Dead
predecessor corpses do not block the reset and are not mutated by scheduling.
On the valid transition, the rule returns exactly one due time, and the
encounter service assigns it only to Uklesh. Khalum and Aztur remain
successor-only.

`ClearRuntimeState` clears all three identities, combat/nano state, and due
times. Re-entry into a still-live playfield preserves the one pending runtime
timer; retirement or replacement cancels it.

## Temple nano inventory

### Active named, successor, add, and Murial domains

| Caster/domain | Nano | Decisive raw evidence | Target/effect ownership | Runtime result |
|---|---:|---|---|---|
| Defender of the Three | 205389 | `20260721-033006`, ordinal 2365, target `7984B398` | Cast/finish packet only; downstream damage/stat source is not isolated | Scheduled packet-only |
| Defender of the Three | 205561 | `20260721-033006`, cast ordinal 2260 and interrupt ordinal 2265; completed casts in `20260721-034700`, ordinals 215/320 and 410/523 | Cast/finish packet is scheduled. The capture-local interrupt variant is retained as evidence but is not replayed without its situational interrupt trigger. | Scheduled finish packet-only; interrupt trigger fail-closed |
| Defender of the Three | 209924 | `20260721-033006`, ordinal 2234, self-target | One identity observation; cadence/effect unresolved | Recognized, unscheduled |
| Windcaller Yatila | 205600 | `20260721-041439`, ordinal 2323 | Target column absent in the capture projection; effect unresolved | Captured ID order; policy-owned 5s initial/10s repeat; packet-only |
| Windcaller Yatila | 205594 | `20260721-041439`, ordinal 2501 | Same boundary | Captured ID order; policy cadence; packet-only |
| Windcaller Yatila | 205592 | `20260721-041439`, ordinal 4833 | Same boundary | Captured ID order; policy cadence; packet-only |
| Reverend Gulard | 205584 | `20260721-042139`, ordinal 1337, self-target row | Nearby positive health changes cannot be uniquely attributed | Captured 15.4s initial; policy-owned 60s repeat; packet-only |
| The Re-Animator | 205592 | `20260721-042705`, ordinal 295 | Single packet identity; schedule/effect incomplete | Recognized, unscheduled |
| The Re-Animator | 205604 | `20260721-043204`, ordinal 3865 | Completion is correlated to one missing Reanimated Corpse slot | Scheduled; exact add-lifecycle gameplay |
| Reanimated Corpse adds | none | No add-owned nano chain | Owner-created combat add only | No nano |
| Acolyte Betany | 205383 | `20260721-044256`, ordinal 568 | Packet structure/timing captured; downstream effect unresolved | Scheduled packet-only |
| The Curator | 205565 | `20260721-225404`, cast/finish ordinals 1653/1721 and 1778/1838, target `70CBBEF3` | Packet structure/timing captured; downstream effect unresolved | Scheduled packet-only |
| Nematet | 205395 | `20260721-225743`, ordinal 1715 | Target not preserved by projection; effect unresolved | Scheduled captured cycle, packet-only |
| Nematet | 205563 | `20260721-225743`, cast/finish ordinals 1785/1818, then 1859/1895, target `70CBBEF3` | Packet structure/timing captured; effect unresolved | Scheduled captured cycle, packet-only |
| Nematet | 205592 | `20260721-225743`, cast/finish ordinals 2173/2201, target `70CBBEF3` | Packet structure/timing captured; effect unresolved | Scheduled captured cycle, packet-only |
| Guardian of Tomorrow | none | No Guardian-owned nano chain | None | No nano |
| Gartua the Doorkeeper | 205590 | `20260721-230824`, ordinal 769, self-target | Self target and timing captured; stat effect unresolved | Scheduled packet-only |
| Uklesh | 204830 | `20260721-231151`, ordinal 1795; 66 observations across that session and `20260722-045835` | Identity is certain; target, downstream effect, and safe reusable cadence are not | Recognized, unscheduled |
| Khalum | none | No Khalum-owned nano chain | None | No nano |
| Aztur | none | No Aztur-owned nano chain | None | No nano |
| Murial the Faithful | 70294 | `20260721-231151`, ordinal 194, ally target; `20260721-232051`, ordinal 16416, self-target | Both self and ally targets occur; selector, cadence, effect, stacking, and expiry remain unresolved | Recognized, unscheduled |

The complete scheduled-timing and downstream-effect disposition is:

| Caster / nano | Initial / repeat / cast seconds | Effect, stat, tick, resist, duration, and stacking result | Cleanup ownership |
|---|---|---|---|
| Defender `205389` | `1.147246 / 10.272 / 5.28395` | Cast/finish packets only. No uniquely attributed effect packet, stat update, damage/heal, periodic tick, skill check, resist, duration, strain, overwrite, refresh, or removal contract. | Pending presentation is cleared on death, encounter reset, and runtime disposal. |
| Defender `205561` | Same cycle; completed-cast duration `6.1904` | Cast/finish packet-only. `20260721-033006` also proves an interrupt packet (`action 108`, parameters `205561/4`), but not the situational trigger or action-117 target selector, so that branch remains fail-closed. | Same pending-presentation cleanup. |
| Defender `209924` | Unscheduled | Self-target identity only; every downstream and repeat field is unresolved. | No runtime state is created. |
| Yatila `205600/205594/205592` | Policy `5 / 10`; captured cast durations `5.96 / 4.945 / 5.0` | Captured ID order and presentation only; target and every downstream gameplay field are unresolved. | Pending presentation is cleared on death, reset, and disposal. |
| Gulard `205584` | Captured initial/cast `15.4 / 4.562`; policy repeat `60` | Self-target cast/finish only. Nearby healing/stat changes lack unique source attribution; duration, stacking, refresh, and removal remain unresolved. | Pending presentation cleanup only. |
| Re-Animator `205604` | `21.718 / 10.291 / 7.04` | Cast/finish plus the exact encounter-owned request for one missing Reanimated Corpse slot at `+1.578s`; no unrelated stat/nano effect is claimed. | Owner death/reset/disposal cancels pending casts and add work and detaches living adds. |
| Re-Animator `205592` | Unscheduled | Packet identity only; target, cadence, effect, duration, and stacking are unresolved. | No runtime state is created. |
| Betany `205383` | `6.444 / 10.116 / 5.337` | Cast/finish packet-only; all downstream gameplay fields unresolved. | Pending presentation cleanup only. |
| Curator `205565` | `15.4643854 / 10.1841983 / 6.2402402` | Captured local-player target and cast/finish packets; all downstream gameplay fields unresolved. | Pending presentation cleanup only. |
| Nematet `205395/205563/205592` | `1.1071981 / 10.1701624`; casts `5.2211694 / 5.6058988 / 3.6813144` | Captured local-player target, ID order, and finish packets; effect/stat/tick/resist/duration/stacking/removal unresolved. | Pending presentation cleanup only. |
| Gartua `205590` | `1.3091279 / 41.5473945 / 0.960617` | Captured self-target cast/finish only; stat effect and lifetime unresolved. | Pending presentation cleanup only. |
| Uklesh `204830` | Unscheduled | Identity is repeated, but target, safe cadence, gameplay effect, duration, and stacking are unresolved. | No runtime state is created. |
| Murial `70294` | Unscheduled | Both self and ally targets are observed, leaving the selector ambiguous; cadence, effect/stat/tick/resist/duration/stacking/removal are unresolved. | No nano worker exists; shared death/reset/disposal still clears Murial combat and movement ownership. |

### Active ordinary PF1931 domains

The active ordinary provider contains seven exact-name `Cultist` MonsterData
families, Eternal Sentinel, Deathless Legionnaire, and Murial.

| Active family | Capture-owned nano evidence | Final disposition |
|---|---|---|
| Cultist MD26074 | No exact-name Cultist cast. The same MonsterData appears under Acolyte Bryant, Pallen, and Reverend Saxx with incompatible nano families `49744/100198/157742`, `81829`, and `205600`. | No selector; disabled |
| Cultist MD26082 | No exact-name cast in the reviewed Temple sessions | Disabled |
| Cultist MD26103 | Exact-name Cultists use `49744`, `100198`, and `157742` across L21..L35. The first decisive pair is `20260721-032247` ordinals 44 and 55. | Multiple interacting nano chains; exact production target/effect/schedule domain is incomplete; disabled |
| Cultist MD26135 | One exact-name `301424` observation in `20260722-042930`, ordinal 1445 | No complete cast/finish/effect/cadence chain; disabled |
| Cultist MD26137 | No exact-name Cultist cast. Caska the Faithful with the same MonsterData uses `81829/82033`. | Name/domain mismatch; disabled |
| Cultist MD26147 | Exact-name Cultists use `205379` repeatedly and one actor uses `301406/301424` in `20260722-042930`. | Multiple generation-local variants with no authoritative selector/effect owner; disabled |
| Cultist MD26149 | Exact-name Cultists use `205580`; first decisive row is `20260721-032547`, ordinal 84, targeting another NPC. | Ally selector, effect, duration, stacking, and removal unresolved; disabled |
| Eternal Sentinel MD41690 | No Sentinel-owned nano chain | No nano |
| Deathless Legionnaire MD42981 | No active-domain nano chain | No nano |
| Murial MD26090 | Nano `70294`, as classified above | Disabled pending categorical selector/effect proof |

For every disabled ordinary row above, no uniquely attributable effect packet,
stat or nano-pool update, damage/heal/tick, skill check, resist result,
duration, stacking group, overwrite, refresh, expiry, or removal chain was
recovered for the exact active categorical domain. No schedule is installed,
so death, reset, and runtime disposal have no orphan nano state to clean up.
The nano database and generic ordinary support runtime can execute those fields
only after the categorical target and full effect contract are known; neither
MonsterData overlap nor a same-name observation supplies that selector.

Other captured Temple residents such as Acolyte Amber, Acolyte Bryant,
Acolyte Felid, Acolyte Kalen, Acolyte Kellian, Acolyte Opet, Caska the
Faithful, Cyth the Faithful, Malikai the Faithful, Nathan the Faithful, Oran
the Faithful, Pallen the Faithful, Reverend Dashell, Reverend Saxx, Exarch
Ecclese, Exarch Gevarain, Exarch Li-Po, Exarch Pilvar, Exarch Truan,
Windcaller Donnel, Windcaller Rendal, Windcaller Tilla, Windcaller Yen,
Deranged Mindreaver, Eternal Guardian, and Sanoo are visible capture evidence
but are not active rows in the authoritative PF1931 runtime population. Their
nanos cannot be enabled through an unrelated active Cultist profile merely
because MonsterData overlaps.

Rows whose source identity was absent from the enemy dossier were excluded as
unattributed; player-owned or observer-owned casts were not promoted to NPC
behavior.

### Effect boundary

The generic `NanoEventRuntimeService.ExecuteOnUseEvents` invokes OnUse events
with the same character as both source and target. It therefore cannot safely
apply hostile or ally-target Temple effects merely from a nano ID. The ordinary
support-nano runtime can represent exact target choice, duration, strain,
modifiers, periodic work, refresh, and cleanup, but it requires those fields to
be proven per categorical domain.

Consequently:

- exact captured cast/finish presentation remains enabled where already
  scheduled;
- Re-Animator `205604` remains the only named nano with gameplay ownership;
- no stat, damage, heal, resist, duration, stacking, refresh, or removal effect
  was fabricated;
- death, reset, and runtime disposal clear pending named casts and
  encounter-owned add work;
- unscheduled identities are catalogued but cannot emit.

## Murial lifecycle

Murial remains a single ordinary-population-owned patrol actor:

- source `0x7987F12D`, MD26090, L34;
- exact 20-waypoint patrol from `20260721-232051` and
  `20260721-234614`;
- combat interrupts the shared patrol owner, and the existing return/reset path
  resumes it;
- corpse CATMesh `5927`;
- corpse timing `30s` born-empty, `180s` unlooted, `30s` after becoming
  empty;
- no proven item or credit outcome, so loot remains fail-closed;
- explicit policy key
  `totw.named.murial.300-after-npc-despawn-policy`;
- `300s` post-NPC-despawn policy delay;
- respawn at the original captured anchor with health, movement, and aggression
  reset;
- the population controller creates one actor and the existing movement owner
  creates one patrol worker;
- runtime reuse cannot rematerialize the row, while runtime disposal cancels
  combat, movement, respawn, corpse, and visibility ownership.

The 300-second value is production-policy-owned, not claimed as a captured
Murial timing. Nano `70294` remains disabled for the exact reasons in the nano
matrix.

## All 18 active named-domain respawn classifications

| PF | Domain | Classification and trigger |
|---:|---|---|
| 127 | Abmouth Supremus | Exact independent encounter respawn, 600s from death |
| 127 | Vergil Aeneid | Exact independent encounter respawn, 600s from death |
| 127 | Eumenides | Explicit 600s death-based runtime policy from Mike's repeated official-live observation; the conflicting `310.001s` cross-session identity interpretation remains evidence-only and does not override policy |
| 127 | Abmouth Infector slots | Owner-only; initial 1.212281/2.326367s and captured refill cycle while Abmouth owns the encounter; never independent |
| 1931 | Defender of the Three | Captured independent replacement, 600s after NPC despawn |
| 1931 | Windcaller Yatila | Explicit Temple policy, 600s after NPC despawn |
| 1931 | Reverend Gulard | Explicit Temple policy, 600s after NPC despawn |
| 1931 | The Re-Animator | Explicit Temple policy, 600s after NPC despawn |
| 1931 | Reanimated Corpse slots | Re-Animator-owned only; 1.578s after `205604` completion or 1.123s after requested dead-add despawn; never independent |
| 1931 | Acolyte Betany | Explicit Temple policy, 600s after NPC despawn |
| 1931 | The Curator | Explicit Temple policy, 600s after NPC despawn |
| 1931 | Nematet | Explicit Temple policy, 600s after NPC despawn |
| 1931 | Guardian of Tomorrow | Captured independent replacement, 600s after NPC despawn |
| 1931 | Gartua the Doorkeeper | Captured independent replacement, 600s after NPC despawn |
| 1931 | Uklesh | Initial chain stage; no independent respawn. Aztur despawn schedules one Uklesh at +600s |
| 1931 | Khalum | Successor-only, 0.6822027s after Uklesh death |
| 1931 | Aztur | Successor-only, 0.211s after Khalum death; its later NPC despawn owns the full-chain reset |
| 1931 | Murial | Explicit PF1931 production policy, 300s after NPC despawn |

The named service no longer uses a blanket suppression boolean. Every Temple
named stage carries an explicit respawn mode. Successor and owner-add modes
return no independent delay.

## Loot probabilities and corpse lifetimes

### Ordinary actors

PF127's 322 ordinary rows retain the established one-roll-per-death loot
runtime. Twenty-one reviewed strict first-open domains provide exact finite
observed item sets. Their runtime entry rolls are independent
existing-capture-policy rolls; the weights are not claimed as official
probabilities, and no entry is mutually exclusive unless an atomic snapshot
below says so. Every successful ordinary entry keeps the captured low/high
template pair and exact QL with quantity one. Pool completeness remains false.
Reopening a corpse and playfield re-entry reuse the same materialized inventory
and never reroll.

| PF127 strict domain | Complete / positive / empty first opens | Exact item/QL owner | Evidence classification |
|---|---:|---|---|
| Discarded Pet MD17720 | `16 / 13 / 3` | `CapturedSubwayOrdinaryContentProvider.cs:376-403` | listed items proven possible; empty proven; wider pool and official probabilities unresolved |
| Bloodcreeper MD30379 | `4 / 1 / 3` | `:404-421` | QL30 `42640/42641` possible; empty proven; wider pool incomplete |
| Shadow MD30464 | `15 / 8 / 7` | `:422-446` | listed items possible; empty proven; wider pool/probabilities unresolved |
| Infector MD31909 | `14 / 6 / 8` | `:447-471` | listed items possible; empty proven; wider pool/probabilities unresolved |
| Infected Attendant MD96056 | `6 / 5 / 1` | `:472-496` | listed items possible; empty proven; wider pool/probabilities unresolved |
| Lost Thought MD96193 | `5 / 3 / 2` | `:497-515` | listed items possible; empty proven; wider pool/probabilities unresolved |
| Uncontrollable Anger MD96195 | `4 / 4 / 0` | `:516-537` | listed items possible; no empty observed; incomplete pool prevents a guaranteed claim |
| Premature Pattern MD203727 | `5 / 4 / 1` | `:538-556` | listed items possible; empty proven; wider pool/probabilities unresolved |
| Incomplete Rebuild MD203728 | `2 / 2 / 0` | `:557-573` | listed items possible; no empty observed; no item is promoted to guaranteed |
| Fragmented Soul MD203729 | `4 / 4 / 0` | `:574-593` | listed items possible; no empty observed; incomplete pool |
| Neural Burnout MD203730 | `6 / 4 / 2` | `:594-616` | listed items possible; empty proven; incomplete pool |
| Empty Shell MD203731 | `5 / 4 / 1` | `:617-637` | listed items possible; empty proven; incomplete pool |
| Violent Vagabond MD203733 | `14 / 13 / 1` | `:638-672` | listed items possible; empty proven; incomplete pool |
| Mugger MD203734 | `18 / 15 / 3` | `:673-713` | listed items possible; empty proven; incomplete pool |
| Deranged Shopper MD203736 | `3 / 3 / 0` | `:714-732` | listed items possible; no empty observed; incomplete pool |
| Stim Fiend MD203739 | `13 / 13 / 0` | `:733-765` | listed items possible; no empty observed; incomplete pool |
| Architect Striker MD203743 | `6 / 5 / 1` | `:766-787` | listed items possible; empty proven; incomplete pool |
| Looter MD203745 | `11 / 6 / 5` | `:788-811` | listed items possible; empty proven; incomplete pool |
| Melded Patterns MD203747 | `4 / 3 / 1` | `:812-831` | listed items possible; empty proven; incomplete pool |
| Workman Striker MD203854 | `30 / 22 / 8` | `:832-876` | listed items possible; empty proven; incomplete pool |
| Redundant Scan MD204178 | `2 / 1 / 1` | `:877-898` | listed items possible; empty proven; incomplete pool |

Only validated first-open inventory updates enter those definitions.
Capture-truncated, later-reopen, unrelated-container, and identity-ambiguous
rows remain excluded. `CorpseInventoryService` materializes the independent
rolls once at death, serves that same state on every open, and removes each
transferred slot once; reopen, re-entry, reset, and disposal cannot create a
second roll.

PF1931 ordinary results remain:

- Cultist MD26074: 9 complete opens, 5 empty, observed QL1 quantity-one item
  identities `204571`, `204711`, `204720`, and `204721`.
- MD26082: 10/10 empty.
- MD26103: 10/10 empty.
- MD26135: 8 opens, 4 empty, observed QL1 quantity-one items `204711` and
  `204712`.
- MD26137: 9 opens, 6 empty, observed QL1 quantity-one items `204571` and
  `204712`.
- MD26147: 13 opens, 10 empty, observed QL1 quantity-one items `204571`,
  `204720`, and `204721`.
- MD26149: 15 opens, 12 empty, observed QL1 quantity-one items `204712` and
  `204721`.
- Eternal Sentinel: 5/5 empty; exact level-credit observations retained.
- Deathless Legionnaire: 19 opens, 15 empty; observed QL1 quantity-one item
  `204746`;
  exact level-credit rules retained.
- Murial: no proven item or credit outcome; no loot is generated.

These Temple ordinary entries are independent capture-frequency policy rolls,
not official probabilities or guaranteed drops. Empty outcomes are proven for
every listed domain; wider pools remain incomplete. The exact definitions are
owned by `CapturedTempleOfThreeWindsContentProvider.BuildLoot` and
`BuildDeathlessLoot`.

All PF127 ordinary corpses retain their established 60-second unlooted and
immediate-empty policy. PF1931 Cultists, Sentinels, and Legionnaires retain
`30/120/30`; Murial retains `30/180/30`.

### Named and encounter-owned actors

| Domain | Unlooted / post-loot seconds | Exact atomic item snapshots (`low/high@QL x quantity`) and credits |
|---|---:|---|
| Abmouth | 1800 / 1800 | 587 credits. `20260712-232137`: `136622/136623@30x1`, `202717/202718@28x1`, `107933/107934@23x1`, `85693/27389@30x1`, `287146@200x1`. `20260716-220400`: `202741/202742@32x1`, `202734/202735@32x1`, `202717/202718@32x1`, `85723/85722@32x1`, `123968/123970@25x1`, `287146@200x1`. |
| Vergil | 1800 / 1800 | `20260712-232711`, 610 credits: `301713@1x1`, `202743/202744@32x1`, `287146@200x1`. `20260712-234401`, 587: `301714@1x1`, `123571/123572@23x1`, `287146@200x1`. `20260716-034433`, 563: `202734/202735@33x1`, `301715@1x1`, `160051/160050@24x1`, `21605@1x100`, `287146@200x1`. |
| Eumenides | 1800 / 3 | 186 credits. `20260717-214751`: `163430/163431@22x1`, `301714@1x1`, `287146@200x1`. `20260717-215250`: `301715@1x1`, `160051/160050@16x1`, `287146@200x1`. The observed 0.660..1.960s final-transfer disappearance supports the shared 3s empty bound. |
| Abmouth Infector | 120 / 30 | Fixed captured 150 credits; no proven item pool. |
| Defender | 120 / 1.277 | 1450 credits. First snapshot: `204750@1x1`, `204649@1x1`; second: `204750@1x2`, `204649@1x1`. |
| Yatila | 120 / 1.640 | 424 credits: `275083@1x1`, `204595@1x1`, `204829@390x1`, `204653@1x1`, `204596@1x1`. |
| Gulard | 120 / 1.772 | Two identical 776-credit snapshots, each `204750@1x1`. |
| Re-Animator | 120 / 1.7 policy | 2357 credits: `275083@1x1`, `204598@1x1`, `204708@1x1`, `204698@1x1`. |
| Reanimated Corpse add | 120 / 1.7 policy | No independently proven item or credit table. |
| Betany | 120 / 1.7 policy | 634 credits: `291082/291083@32x50`, `291043/291044@32x25`, `204572@1x1`. |
| Curator | 120 / 1.7 policy | 377 credits: `287143@200x1`, `204758@1x1`, `204651@1x1`. |
| Nematet | 120 / 1.7 policy | 2711 credits: `287143@200x1`, `204651@1x1`, `204706@1x1`, `204595@1x1`. |
| Guardian | 1800 / 1.7 policy | 2830 credits: `287143@200x1`, `204596@1x1`, `204756@1x1`, `204601@1x1`. |
| Gartua | 120 / 1.7 policy | 1592 credits: `204650@1x1`, `204598@1x1`. |
| Uklesh | 120 / 1.7 policy | 625 credits: `204757@1x2`, `204653@1x1`. |
| Khalum | 120 / 1.7 policy | 625 credits: `204608@1x2`, `204598@1x1`. |
| Aztur | 120 / 1.7 policy | 3184 credits: `287143@200x1`, `204593@1x2`, `204755@1x1`, `204608@1x1`. |
| Murial | 180 / 30 | No proven item or credit outcome. |

Temple named loot definitions preserve complete observed corpse inventories as
atomic snapshots. Selection probability is explicitly `Unresolved`; entries
retain zero weight and zero claimed drop chance. The runtime does not combine
items from different outcomes, make a positive observation guaranteed, or
invent an empty probability. Each snapshot comes from a corpse open followed
by exact inventory rows and linked one-time transfers. Session-truncated,
reopen-only, identity-ambiguous, or unrelated inventory updates remain
non-promotable. Corpse state owns the selected snapshot through reset and
re-entry until transfer or expiry.

Bloodcreeper's separately accepted four-open constraint remains unchanged:
one QL30 `42640/42641` outcome and three empty outcomes, with the wider pool
still incomplete.

## Shared ownership and cleanup

- Encounter services own named actor identities, pending nanos, successors,
  and adds. One shared `DungeonNamedRespawnScheduler`, backed by the established
  `WorldRespawnScheduler`, owns PF127/PF1931 named respawn, successor, and reset
  due times with profile uniqueness and playfield-scoped cancellation.
- World population owns ordinary actors, Murial's generation, patrol,
  corpse notification, and respawn schedule.
- Corpse inventory rejects duplicate dead-NPC ownership before a second global
  roll and owns the one materialized item/credit state.
- Corpse lifecycle owns loot-bearing and empty retirement.
- Visibility owns registration and removal.
- Runtime retirement calls each owner's reset path. Pending combat, movement,
  nanos, add work, successor/reset times, respawns, corpses, loot state, and
  visibility cannot execute from a retired playfield runtime.
- PF127 and PF1931 registries remain isolated by playfield instance.

The task-specific 19-domain classification, main-room phase model, Murial
result, corpse/loot matrix, worker ownership matrix, and exact remaining
boundaries are recorded in
`docs/evidence/DUNGEON_NAMED_LIFECYCLE_COMPLETION_20260729.md`.

## Final exact unresolved behaviors

1. Temple packet-only nano downstream stat/damage/heal/resist/stacking behavior
   remains disabled where raw and generic ownership cannot establish it.
2. Defender `209924`, Re-Animator `205592`, Uklesh `204830`, Murial `70294`,
   and the listed active ordinary nano families remain unscheduled when their
   complete categorical selector, target rule, cadence, or effect contract is
   absent.
3. Murial item/credit loot remains unproven.
4. Named and ordinary wider loot pools and official selection probabilities
   remain unresolved unless explicitly identified above as authoritative.
5. The post-Aztur 600-second delay is inherited from the explicit Temple named
   policy; no capture of the entire Aztur-to-Uklesh reset interval exists.

## Validation

### Deterministic generation

- Capture analyzer self-test: PASS.
- Lifecycle decoder self-test: PASS.
- Subway combat generator: two identical PASS runs, `40` archetypes.
- Full bounded-memory combat generator write: PASS in `274.5s`.
- Full bounded-memory combat generator check: PASS in `314.1s`, no diff.
- Both full runs produced `375` sessions, `359` canonical sessions, `2,827`
  complete chains, `255` certified profiles, `95` runtime-ready profiles,
  `303` semantic definitions, `100` runtime-ready definitions, `1,404`
  unresolved profiles, and zero errors.
- Active coverage write/check: PASS, `1,512` actors, `312` certified, `1,200`
  unresolved across its broader fixed and non-denominator surfaces. Ordinary
  dungeon combat remains independently complete at `489/489`.

### Focused and full tests

- Named dungeon lifecycle completion: PASS, `20/20`.
- Named dungeon encounter completion: PASS, `10/10`.
- Temple ordinary content regression: PASS, `7/7`.
- Abmouth/Subway named encounter regression: PASS, `27/27`.
- Strike Foreman focused combat/lifecycle/loot: PASS, `3/3`.
- Shared captured packet factory: PASS, `38/38`.
- Combat profile catalog: PASS, `51/51`.
- Global loot foundation: PASS, `10/10`.
- Eumenides corpse lifecycle regression: PASS, `1/1`.
- Active coverage: PASS, `3/3`.
- Clean synchronized-start worktree baseline: `644/723` PASS with `79`
  pre-existing failures.
- Clean task-only staged result: `653/728` PASS with `75` pre-existing
  failures. The five added tests all pass and four prior failures are repaired;
  there is no new clean-worktree failure. The remaining failures are the
  separated damage-policy, mission/inventory ownership, absent clean-worktree
  geometry/capture assets, Arete/content deployment, route-guardrail, and
  stale visibility-count baselines.
- Primary workspace after the concurrent mission commit: `694/731` PASS with
  the same `37` unrelated failures. During that concurrent edit the test
  project temporarily failed compilation while
  `MissionAcgAcceptedQfuBuilder` and
  `QuestFullUpdateMessageSerializer.FixedStringBytes` were being added; the
  completed concurrent commit resolves that transient state. This task's
  focused suites and clean result remain separately authoritative.

### Production and runtime

- ZoneEngine Debug production build: PASS after the approved engine stop
  released the prior output lock.
- `git diff --check`: PASS.
- Git LFS integrity: PASS.
- Approved engine restart: PASS. Ports `6996`, `7012`, `7500`, and `7501`
  are listening.
- No AO client was launched. Runtime acceptance is server-side deterministic
  test and engine-start validation, not a live-client claim.
