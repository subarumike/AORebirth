# Dungeon nano and loot contract completion

> **PF1931 status authority (2026-08-01):** Historical evidence/provenance only. Current PF1931 status is the [Temple full-corpus completion matrix](TEMPLE_FULL_CORPUS_COMPLETION_20260801.md); any PF1931 completion, blocker, or test-count statement below is superseded by that matrix.

Date: 2026-07-30

Starting repository SHA: `1a6f63253f26928f9a91581bdbe7b028191bd7be`.

The requested baseline SHA `47a49383c1c0a1484614b10e817d246948cb022c`
was not the checked-out synchronized master when this pass started. Both local
master and `origin/master` were at the starting SHA above.

## Scope and evidence

This pass covers the remaining PF127 and PF1931 nano, named loot, credits,
corpse-inventory, and probability contracts. It does not change ordinary or
named weapon combat, respawn, the Aztur chain reset, Murial patrol ownership,
or mission code.

Authoritative inputs were:

- the finalized dungeon captures already classified in
  `DUNGEON_GAMEPLAY_COMPLETION_20260728.md`;
- raw Murial rows in `20260721-231151` and `20260721-232051`;
- the client `18.8.62_EP1` `nanos.dat`, decoded through the existing
  `PerkActionExtract` nano loader;
- the capture-owned item identities, quantities, QLs, and credits already
  encoded in `GlobalLootRuntimeService`,
  `CapturedTempleOfThreeWindsLootDefinitions`, and
  `CapturedTempleOfThreeWindsContentProvider`;
- the shared `NanoEventRuntimeService`, `CorpseInventoryService`, and global
  loot materialization runtime.

No probability, item, QL, quantity, credit value, target selector, or nano
schedule was inferred from sparse observations.

## Nano inventory

### Gameplay-complete

| Domain | Nano | Exact implemented contract |
|---|---:|---|
| Reverend Gulard | 205584 | Capture-proven self target; existing `15.4s` initial, `60s` repeat policy, and `4.562s` cast finish. Nano data has no attack or defend check, zero duration, range 40, and one target-health `Hit` of `+209..+252`. At successful finish the runtime executes that exact OnUse event through `NanoEventRuntimeService`; no persistent state exists to stack or leak. Death/reset/disposal already cancel pending casts. |
| The Re-Animator | 205604 | Existing capture-owned cast/finish requests exactly one missing Reanimated Corpse slot. Owner death/reset/disposal cancels pending casts and add work and removes owned adds. The unrelated nano-data shape/debuff/damage functions remain disabled because the capture proves add-lifecycle ownership, not a second target-effect chain. |

Guardian of Tomorrow, Khalum, Aztur, Reanimated Corpse adds, Eternal Sentinel,
and Deathless Legionnaire have no active-domain nano chain and explicitly
remain no-nano domains.

### Exact data known, gameplay fail-closed

The following identities and payloads are authoritative, but their missing
runtime boundary prevents safe execution:

| Domain / nano | Authoritative nano-data payload | Exact blocker |
|---|---|---|
| Defender `205389` | Attack `130:100`, defend `168:120`, duration 0, range 40, target-health `-153..-335`, AC 97 | The capture does not isolate resist outcome or exact damage/stat/chat packet ownership. The shared captured-enemy `Hit` path deliberately quarantines offensive nano damage. |
| Defender `205561` | Attack `130:100`, defend `168:120`, duration 0, range 40, AreaCast child `205554` radius 12; child damage `-83..-248`, AC 97 | Area recipient selection, resist results, damage packets, and the capture-local interrupt trigger are incomplete. |
| Yatila `205600` | Attack `129:100`, defend `168:120`, duration 3200cs, range 40; stats `129/122/130/131/127/128` each `-35` | Exact target projection, strain/overwrite behavior, stat-update packets, and reversible cleanup ownership are incomplete. |
| Yatila `205594` | Attack `130:100`, defend `168:120`, duration 6000cs, range 40; two health hits of `-15` with AC 92 and stat 318 `+12` | Damage/stat packet ownership, refresh/expiry removal, and the complete target contract are incomplete. |
| Yatila, Re-Animator, and Nematet `205592` | Attack `130:100`, defend `168:120`, duration 1600cs, strain 146, range 40; health `-40..-190`, AC 95, RestrictAction 4 | The shared generic path cannot safely own the offensive hit, action restriction, resist result, refresh, and cleanup as one exact contract. Re-Animator's occurrence is also unscheduled. |
| Betany `205383` | Attack `130:100`, defend `168:120`, duration 0, range 40; health `-93..-222`, AC 97 | Resist and offensive damage packet ownership are not captured. |
| Curator `205565` | Attack `130:100`, defend `168:120`, duration 0, range 40, AreaCast child `205556` radius 12; child duration 300cs, strain 147, damage `-97..-280`, AC 97, Stun | Area recipient selection, resist/damage packets, stun stacking/refresh, and removal are incomplete. |
| Nematet `205395` | Attack `130:100`, defend `168:120`, duration 0, range 40, CastNano child `205378`; child duration 300cs, strain 146, damage `-186..-407`, AC 97, Stun | Child target, resist/damage packet ownership, stun refresh, and removal are incomplete. |
| Nematet `205563` | Attack `130:100`, defend `168:120`, duration 0, range 40, AreaCast child `205555` radius 12; child damage `-96..-288`, AC 97 | Area recipient selection and resist/damage packet ownership are incomplete. |
| Gartua `205590` | No attack or defend check, duration 3500cs, range 1, wearer target; health `+35`, stat 1 `+350`, stat 360 `+30`, stat 276 `+80`, and stats `279/280/278/316/311/317/281/282` each `+30` | The generic Modify function is additive and does not reverse these eleven modifiers. Replaying it without a shared exact multi-stat expiry/refresh owner would permanently stack stats and fail cleanup. |
| Defender `209924` | Duration 600cs, range 1, strain 433; no OnUse event | Only a self-target identity row exists; no executable payload or schedule exists. |
| Uklesh `204830` | Attack `131:100`, defend `168:100`, duration 500cs, range 1, target Stun | Target selection and safe cadence are unresolved; stun refresh/removal packet ownership is incomplete. |

These scheduled named nanos retain their existing exact cast and finish packets.
No visual cast is promoted to gameplay where the complete effect contract is
missing.

### Murial 70294

The result is explicitly fail-closed:

- `20260721-231151` proves Murial `79872FC2` casts 70294 on Windcaller Tilla
  `7987F023`.
- `20260721-232051` proves Murial `7987F0C1` casts it on Windcaller Tilla
  `7987F0C9`, and also contains repeated self-target casts by Murial identities
  `7987F0C1` and `7987F12D`.
- Nano data proves attack skills `130:50` and `131:50`, no defend check,
  duration `1440000cs`, range 20, and strain 2.
- Its target Modify payload is stats
  `205/206/207/208/216/217/219/225 +10` and
  `475/476/477/478/479/480/482/483 +3`.

The exact blocker is the categorical ally-versus-self selector and safe cast
cadence. The captures include both target classes and self recasts inside the
four-hour data duration, but do not prove how an eligible recipient is chosen
or when refresh is allowed. In addition, the generic Modify path does not
reverse the 16 deltas on expiry/death/reset. Therefore no Murial nano worker,
modifier, timer, or active-nano state is created.

### Active ordinary PF1931 nanos

All recoverable nano-data payloads were decoded, but no additional ordinary
domain has a complete categorical selector and schedule:

| Active domain | Observed nano data | Final disposition |
|---|---|---|
| Cultist MD26074 | No exact-name cast; same MonsterData named actors use incompatible `49744/100198/157742`, `81829`, or `205600` families | Disabled: name/domain mismatch |
| Cultist MD26082 | No exact-name cast | No nano |
| Cultist MD26103 | `49744` is a 4700cs self/wearer multi-stat percentage buff that triggers `157742`; `100198` is a 2000cs self heal plus radius-20 AreaCast; `157742` is a 2000cs six-stat `-4000` effect | Disabled: generation/level selector, chain order, cadence, area recipients, stacking, and cleanup are incomplete |
| Cultist MD26135 | `301424`, duration 12000cs, strain 135, 23 stats each `-21` | Disabled: one observation does not prove schedule, target, refresh, or removal |
| Cultist MD26137 | No exact-name cast; Caska with the same MonsterData uses `81829/82033` | Disabled: name/domain mismatch |
| Cultist MD26147 | `205379` target damage `-77..-136`, plus generation-local `301406/301424` 12000cs multi-stat debuffs | Disabled: authoritative generation selector and complete effect ownership are absent |
| Cultist MD26149 | `205580`, no attack/defend check, duration 0, target heal `+120..+162` | Disabled: ally target choice and cadence are unresolved |
| Eternal Sentinel MD41690 | No active-domain nano chain | No nano |
| Deathless Legionnaire MD42981 | No active-domain nano chain | No nano |
| Murial MD26090 | 70294 as classified above | Disabled with exact selector/cadence/cleanup blocker |

Because disabled rows install no timer or effect state, death, reset, re-entry,
and runtime disposal cannot leave an orphan nano worker.

## Loot, credits, corpse inventory, and probability inventory

### Named outcomes implemented

Every proven named outcome is already materialized once per death as one
atomic corpse snapshot. Reopen and re-entry read the same corpse inventory;
transfer removes only the transferred slot; reset, expiry, and runtime
replacement retire the owned corpse state.

| Domain | Proven atomic result |
|---|---|
| Abmouth | 587 credits; 2 captured atomic item snapshots |
| Vergil Aeneid | 610, 587, or 563 credits bound to 3 captured atomic snapshots |
| Eumenides | 186 credits; 2 captured atomic snapshots |
| Strike Foreman | 176 credits; 2 captured atomic level-bounded snapshots |
| Abmouth Infector | 150 credits; no proven item pool |
| Defender | 1450 credits; 2 captured atomic snapshots |
| Yatila | 424 credits; 1 captured atomic snapshot |
| Gulard | 776 credits; 2 identical captured atomic snapshots |
| Re-Animator | 2357 credits; 1 captured atomic snapshot |
| Betany | 634 credits; 1 captured atomic snapshot |
| Curator | 377 credits; 1 captured atomic snapshot |
| Nematet | 2711 credits; 1 captured atomic snapshot |
| Guardian | 2830 credits; 1 captured atomic snapshot |
| Gartua | 1592 credits; 1 captured atomic snapshot |
| Uklesh | 625 credits; 1 captured atomic snapshot |
| Khalum | 625 credits; 1 captured atomic snapshot |
| Aztur | 3184 credits; 1 captured atomic snapshot |

The exact item identities, low/high templates, quantities, and QLs remain
unchanged in their capture-owned definitions and are enumerated in
`DUNGEON_GAMEPLAY_COMPLETION_20260728.md`. Strike Foreman's two unchanged
snapshots are `27199@QL10 x1 + 123744/123745@QL20 x1 + 301713@QL1 x1` and
`85676/22072@QL15 x1 + 301707@QL1 x1`.

Reanimated Corpse adds have no independently proven loot or credits. Murial
has no proven item, credit, or confirmed-empty corpse outcome. Both remain
fail-closed with no generated loot; this is not promoted to a claim that their
official tables are empty.

### Ordinary PF127 and PF1931 outcomes implemented

PF127 retains the 21 strict first-open ordinary domains and their exact
captured item/QL/quantity sets:

Discarded Pet, Bloodcreeper, Shadow, Infector, Infected Attendant, Lost
Thought, Uncontrollable Anger, Premature Pattern, Incomplete Rebuild,
Fragmented Soul, Neural Burnout, Empty Shell, Violent Vagabond, Mugger,
Deranged Shopper, Stim Fiend, Architect Striker, Looter, Melded Patterns,
Workman Striker, and Redundant Scan.

PF1931 retains the exact captured results for Cultist MD26074, MD26082,
MD26103, MD26135, MD26137, MD26147, MD26149, Eternal Sentinel, and Deathless
Legionnaire, including every proven empty first-open and exact level-credit
rule. Murial remains blocked as described above.

### Probability result

- Proven empty ordinary outcomes remain possible empty results.
- No positive-only sparse sample becomes guaranteed.
- Complete named corpse observations remain indivisible snapshots.
- Snapshot-selection and wider-pool probabilities remain explicitly
  `Unresolved`, with zero claimed weight and zero claimed drop chance.
- Independent ordinary entries remain independent only where the existing
  capture-policy definition says so.
- No reopen, re-entry, reset, or runtime replacement performs a second roll.

## Validation

- Nano-data decode: PASS, client `18.8.62_EP1`, `10965` nanos loaded; all
  listed primary and child payloads resolved by exact ID.
- `DungeonNamedEncounterCompletionTests`: PASS, `10/10`.
- `DungeonNamedLifecycleCompletionTests`: PASS, `20/20`.
- `GlobalLootFoundationTests`: PASS, `12/12`.
- `AbmouthEncounterRuntimeServiceTests`: PASS, `27/27`.
- `TempleOfThreeWindsOrdinaryContentTests`: PASS, `7/7`.
- Debug build: PASS after stopping the prior ZoneEngine process that held the
  output executable open.

The full baseline, lifecycle, diff, synchronization, and restart results are
recorded in the task completion report after final validation.
