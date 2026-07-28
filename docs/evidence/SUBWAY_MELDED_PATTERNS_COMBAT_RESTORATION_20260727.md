# Subway Melded Patterns Mathematical Combat Restoration

Date: 2026-07-28
Resource: PF127
Family: `Melded Patterns`
MonsterData: `203747`
Runtime selector: `subway.ordinary.melded_patterns`

## Result

All ten active Melded Patterns actors now resolve through the shared
capture-backed equipped-weapon path. The six actors quarantined at the task
checkpoint are restored without an identity lookup, per-level output table,
nearest-level substitution, copied neighboring values, or a family-specific
combat loop.

The accepted numeric setup is:

```text
base = floor((11 * actorLevel - 2) / 2)

SpecialAttackWeapon.Unknown1 = base
SpecialAttackWeapon.Unknown2 = base + 28
SpecialAttackWeapon.Unknown3 = base
SpecialAttackWeapon.Unknown4 = base
```

The declared domain is PF127, `Melded Patterns`, MonsterData `203747`, actor
levels `18..25`, equipped attack mode, slot `6`, instance `0`, one normal
stream, hit wire `3`, damage wire `0`, and one of the three owner-selected
weapon-template domains below. Unsupported level, weapon, slot, mode, stream,
hit, or damage semantics fail closed.

## Complete Active Actor Inventory

Every row occurs exactly once in
`CapturedSubwayOrdinaryContentProvider.cs:360..369`. The population row owns
level, QL, health, health damage, scale, run speed, and the owner-linked weapon
loadout. `binding` and `coverage` are the deterministic active-coverage keys.
`Q` means quarantined at the task checkpoint and `C` means certified.

| Source | Level | Actor QL | Owner weapon low/high | Population line | Binding | Coverage | Start | Final |
|---|---:|---:|---|---:|---|---|---|---|
| `0x7954508E` | 23 | 20 | `121818/121818` | 360 | `0a72dc80c14ad2da24ec` | `985f087c44c33121f162` | Q | C |
| `0x7954517C` | 18 | 19 | `121817/121818` | 361 | `4438f4af05a60d4ffb7a` | `54a5e3f5c2315e91ce40` | C | C |
| `0x79545185` | 19 | 18 | `121817/121818` | 362 | `b6e61baade38c660a4ca` | `6a0fd6b7ae25569e64de` | C | C |
| `0x79545187` | 21 | 26 | `121819/121820` | 363 | `cf6917567f6170a88e46` | `637ea641af97bee9f5c3` | Q | C |
| `0x79545190` | 18 | 20 | `121818/121818` | 364 | `8039dfc932e5c2d5d740` | `2d9109c7d637a0a85a5f` | Q | C |
| `0x79545196` | 20 | 20 | `121818/121818` | 365 | `9940751ae0f8a9abcf4f` | `d79f6ed76652d466d60d` | Q | C |
| `0x79545198` | 21 | 20 | `121818/121818` | 366 | `814c7772213c2364f63a` | `f84f35e4c64fa264a9b6` | Q | C |
| `0x795451BA` | 22 | 26 | `121819/121820` | 367 | `2786011d0521e762cb74` | `13ac9331c18290b915bf` | Q | C |
| `0x795451D8` | 25 | 25 | `121819/121820` | 368 | `e447d6a1cf0a093a1d47` | `f28020fde6d30a4b6c8c` | C | C |
| `0x795451DD` | 25 | 19 | `121817/121818` | 369 | `9f50d8b993aa41891dbb` | `b23edfc74adca83936c1` | C | C |

The exact six-actor starting quarantine scope was:

```text
0x7954508E L23
0x79545187 L21
0x79545190 L18
0x79545196 L20
0x79545198 L21
0x795451BA L22
```

The certified reference actors were `0x7954517C`, `0x79545185`,
`0x795451D8`, and `0x795451DD`. Source `0x795451DD` is the previously restored
level-25 actor. Its old aggregate QL20 baseline was incorrect; its authoritative
owner-linked population loadout is QL19 `121817/121818`.

For every active row, damage, damage bonus, range, attack/recharge cadence,
health, attack rating, defense, Energy, ammunition, and other derived or
mutable values remain with the existing spawn, item database, combat rules,
and per-actor runtime state. None is copied from another level.

## Categorical Weapon Domains

`items.dat` proves templates `121817..121835` are one interpolation list.
The runtime actor QL selects exactly one of these three WIFU template domains:

| Actor QL domain | WIFU low/high | Active sources |
|---|---|---|
| `1..19` | `121817/121818` | `0x7954517C`, `0x79545185`, `0x795451DD` |
| `20` | `121818/121818` | `0x7954508E`, `0x79545190`, `0x79545196`, `0x79545198` |
| `21..40` | `121819/121820` | `0x79545187`, `0x795451BA`, `0x795451D8` |

The three tuples are different interpolation positions of one weapon family,
not three attack semantics. Runtime QL uniquely selects the tuple. No
most-common, nearest-level, or source-identity selection occurs.

The capture-bound WIFU and attack semantics common to the domains are:

- equipped mode; WIFU slot `6`;
- state-machine type `1000015`, instance `0`;
- WIFU `Unknown1=11`, `Unknown2=262`, `Unknown3=0`;
- WIFU flags `1027`, `MultipleCount=1`;
- captured WIFU `AttackDelay=235`, `RechargeDelay=235`;
- no serialized weapon name/tag is present; the item interpolation list and
  template tuple are the categorical family key;
- `SpecialAttackWeapon n3=0`, no specials;
- `Attack n3=0`, action `0`;
- exactly one stream, ordinal `0`;
- AttackInfo slot `6`, instance `0`, hit wire `3`, damage wire `0`, n3 `0`;
- exact order `WIFU -> SpecialAttackWeapon -> Attack -> AttackInfo`.

Normal, miss, critical, and terminal results retain their raw classification.
A terminal AttackInfo is an outcome of the one scheduled stream, never a
second repeating stream. Miss attribution uses the embedded attacker and
defender. Interleaved attackers and unattributed target-health transitions are
not merged into Melded Patterns evidence.

## Generated Combat Profiles

Eleven complete semantic profiles validate the formula and the shared packet
shape:

| Level | Semantic profile IDs | Captured SAW values |
|---:|---|---|
| 18 | `a867d5624faafcec-2125d88ca85e181c`, `a867d5624faafcec-8fb16c7c66cd28fc` | `98/126/98/98` |
| 19 | `67f518afac8fd529-88660aa55a7b2d5c` | `103/131/103/103` |
| 20 | `477ae7aca2274b51-474eed6223e4ee5c` | `109/137/109/109` |
| 21 | `507420968010ac73-01ed8f89e4ecb861`, `507420968010ac73-e34cd954337a7dcf`, `507420968010ac73-ef6d2cf09c6524ec`, `507420968010ac73-fc12498b7e77187d` | `114/142/114/114` |
| 24 | `550de529541c8221-239b7c7cd80ed8c4` | `131/159/131/131` |
| 25 | `41ec2f5fb41b8e2f-5f0a16ad1c7c6589`, `41ec2f5fb41b8e2f-95101dc382060622` | `136/164/136/136` |

The active L22 and L23 sources have owner-linked WIFU evidence but no
source-local complete normal chain. Their generated values are respectively
`120/148/120/120` and `125/153/125/125`. They are safe because the categorical
selector is unique, every complete same-family stream has the same packet
semantics, the formula reproduces every observed numeric field exactly, and
the active generation owns the exact QL/template tuple.

## Decisive Raw Sequences

The generated formula dataset records exact UTC timestamps, packet IDs, body
hex, mutable values, and correlated stream fields for every row below.

| Session | Source/level | WIFU | SAW | Attack | AttackInfo |
|---|---|---:|---:|---:|---:|
| `20260709-225408` | `0x79545190` L18 | 9115 | 9811 | 9812 | 9883 |
| `20260709-225408` | `0x7954517C` L18 | 8200 | 8791 | 8792 | 9056, 9286 |
| `20260720-051714` | `0x7980F107` L19 | 7491 | 7903 | 7904 | 7971 |
| `20260709-222339` | `0x79545196` L20 | 7893 | 8730, 8978 | 8731, 8979 | 8741, 8848, 9148 |
| `20260709-222339` | `0x79545187` L21 | 6835 | 7855 | 7856 | 8122 |
| `20260709-225408` | `0x79545198` L21 | 9762 | 10519 | 10520 | 10665, 10857 |
| `20260720-051714` | `0x7980F106` L21 | 7489 | 7914 | 7915 | 7994 |
| `20260720-051714` | `0x7980F149` L21 | 5406 | 7527 | 7528 | 7582 |
| `20260720-051714` | `0x798037DE` L24 | 1037 | 2527 | 2528 | 2706 |
| `20260709-222339` | `0x795451DD` L25 | 11652 | 12674 | 12675 | 12777 |
| `20260709-225408` | `0x795451DD` L25 | 14817 | 15077 | 15078 | 15641, 15766 |
| `20260720-051714` | `0x798037E7` L25 | 1039 | 3127 | 3128 | 3242 |

Capture boundaries, StopFight, CharacterAction `99`, target death, and
interleaved activity are retained in the raw corpus. They are not promoted
into extra reusable streams. L22 source `0x795451BA` has owner WIFU ordinal
5387 in `20260709-222339`; L23 source `0x7954508E` has owner WIFU ordinal 4119
in that session.

## Formula Dataset and Candidate Rejection

`enemy_combat_setup_formula_dataset.json` now includes:

- all 13 exact raw SAW observations across six captured levels;
- all 11 complete semantic profiles;
- partial owner-linked L22/L23 evidence;
- all ten active population bindings and categorical loadouts;
- item-list endpoints and referenced item stats;
- WIFU, SAW, stream, damage, timing, Energy, ammunition, and mutable fields;
- authoritative runtime owners;
- leave-one-out and cross-family boundaries;
- rejected candidate families and mismatch counts.

Candidate results:

| Candidate | Exact mismatch result |
|---|---|
| unrounded `(11L-2)/2` | 3 captured odd levels are half-integers |
| ceiling division | 3 captured odd levels round above raw |
| nearest-away division | 3 captured odd levels round above raw |
| nearest-even division | L19 differs; 1 mismatch |
| four identical SAW fields | Unknown2 differs in all 13 raw packets |
| weapon QL as sole input | 5 conflicts because QL19 and QL20 occur at multiple actor levels with different outputs |
| direct item-template interpolation for SAW | 6 captured-level mismatches; item data owns loadout/gameplay stats, not SAW base |
| one unbounded level domain | rejected because categorical/formula proof ends at L18 and L25 |

The bounded floor formula has zero mismatches across all 13 raw packets.
Leave-one-out validation succeeds for all six captured levels. The existing
Stim Fiend formula independently confirms the same base expression, while the
Melded-specific `Unknown2=base+28` and equipped categorical selector prevent
cross-family reuse. No other family enters the exact MonsterData, equipped
item-list, slot, and stream domain.

## Runtime Ownership

Authoritative inputs and owners are:

- actor level, actor QL, health, and generation-local stats:
  `CapturedSubwayOrdinarySpawnDefinition`;
- family and MonsterData:
  `CapturedSubwayOrdinaryArchetypeDefinition`;
- owner-selected templates and weapon QL:
  atomic generation plus `items.dat`;
- damage, damage bonus, range, attack/recharge cadence, attack skill, and
  defense: existing item/combat production owners;
- Energy, ammunition, SAW Unknown5, and ordered mutable observations:
  existing per-actor combat runtime state.

Capture retains template family, equipped mode, slot, instance, WIFU shape,
SAW shape, packet order, attack action, stream count/order, hit type, damage
type, and result semantics. Production generation is not permitted to alter
those fields.

## Canonicalization and Multi-Tuple Findings

No generic extractor or canonicalization defect was found. The apparent
missing profiles at active L22/L23 are source-local incomplete chains, not
missing categorical data. The apparent incompatible tuples are the three
documented QL domains of one `items.dat` interpolation list. The active
generation QL selects exactly one tuple before binding; source identity is
only evidence provenance and does not participate in formula selection.

Mutable WIFU Energy, AttackInfo ammunition, and SAW Unknown5 caused otherwise
valid profiles to be marked runtime-unsafe by the older exact-observation
path. The mathematical resolver now treats those fields as per-actor state
while retaining every actual packet-semantic distinction.

## Coverage

Before:

- PF127: `264 certified / 58 quarantined`;
- PF1931: `87 / 80`;
- combined: `351 / 138` of `489`;
- Melded Patterns: `4 / 6`;
- fixed 53-actor Subway scope: `26 / 27`.

After:

- PF127: `270 certified / 52 quarantined`;
- PF1931: unchanged `87 / 80`;
- combined: `357 / 132` of `489`;
- Melded Patterns: `10 / 0`;
- fixed 53-actor Subway scope: `32 / 21`.

Exactly six unique actors were restored. No additional family was broadened.
Rejection rows and unique actors remain one-to-one in this family.

## Validation Contract

Focused coverage proves:

- all ten active actors enumerate once;
- all six starting quarantined sources reconcile uniquely;
- every owner-linked QL/template tuple resolves one exact categorical domain;
- all 13 captured SAW bodies remain byte-exact;
- the captured L25 QL19 WIFU, SAW, Attack, and AttackInfo remain byte-exact;
- L22/L23 deterministic fields use only authoritative runtime inputs;
- runtime identity is unnecessary;
- unsupported levels and weapon domains fail closed;
- terminal outcomes do not create extra scheduled streams;
- shared order remains `WIFU -> SAW -> Attack -> AttackInfo`;
- Disobedient Bot and Stim Fiend formulas remain exact.

## Completed Validation

- extractor self-test: PASS;
- full corpus write and second deterministic check: PASS, `374` sessions,
  `358` canonical sessions, `2,827` complete chains, `255` certified profiles,
  `95` runtime-ready profiles, `303` semantic definitions, `100`
  runtime-ready definitions, `1,404` unresolved profiles, `0` errors;
- ordinary setup generator: `16/16` PASS;
- combat catalog: `47/47` PASS;
- packet factory: `37/37` PASS;
- generated exact-byte fixtures: `3/3` PASS;
- active coverage: `3/3` PASS;
- Temple regressions: `6/6` PASS;
- chase/range/cancellation: `38/38` PASS;
- collision/LOS: `17/17` PASS;
- world population: `35/39`; four accepted unrelated baseline failures;
- Subway/Abmouth: `23/26`; three accepted unrelated baseline failures;
- complete messaging suite: `536/575`; exactly the accepted `39` unrelated
  baseline failures and no new failure;
- Debug build: PASS;
- engine restart: PASS;
- ports `6996`, `7012`, `7500`, and `7501`: LISTENING.
