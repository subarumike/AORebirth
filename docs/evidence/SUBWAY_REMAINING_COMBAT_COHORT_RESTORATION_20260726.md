# Subway Remaining Combat Cohort Restoration

Date: 2026-07-26

## Scope

This pass fixed the remaining 53-actor PF127 combat scope identified after the
accepted Violent Vagabond and Filth Flea work. The scope is fixed by runtime
source identity in a regression test; no capture rescan, new contract, generic
fallback, nearest-level selection, or cross-enemy mapping was used.

Before this pass the combined PF127/PF1931 active population was `325`
capture-certified and `164` quarantined. PF127 was `238/84`; PF1931 was
`87/80`.

## Exact 53-Actor Inventory

| Family | MonsterData | Level | Runtime source identities | Actors |
| --- | ---: | ---: | --- | ---: |
| Bloodcreeper | 30379 | 24 | `795451C5` | 1 |
| Disobedient Bot | 17649 | 6 | `7953AFA3` | 1 |
| Disobedient Bot | 17649 | 7 | `7953AF98`, `79557C66` | 2 |
| Disobedient Bot | 17649 | 9 | `7953AD4B`, `7953AD69`, `7953AF6F` | 3 |
| Disobedient Bot | 17649 | 10 | `7953AA1E`, `7953AA81`, `7953AA8F`, `7953AB08`, `7953AD61`, `7957E40A` | 6 |
| Fragmented Soul | 203729 | 17 | `7954516F` | 1 |
| Fragmented Soul | 203729 | 18 | `7954518B`, `7954518E`, `79545367` | 3 |
| Fragmented Soul | 203729 | 19 | `7954517A` | 1 |
| Fragmented Soul | 203729 | 21 | `795451AE` | 1 |
| Incomplete Rebuild | 203728 | 17 | `79545170`, `79545241` | 2 |
| Incomplete Rebuild | 203728 | 18 | `79545172` | 1 |
| Incomplete Rebuild | 203728 | 19 | `79545177`, `79545181`, `79545188` | 3 |
| Looter | 203745 | 9 | `795313CB`, `79545029`, `7957E5CD` | 3 |
| Looter | 203745 | 10 | `795312DC`, `79545034`, `7954503C`, `79557CB8` | 4 |
| Melded Patterns | 203747 | 18 | `79545190` | 1 |
| Melded Patterns | 203747 | 20 | `79545196` | 1 |
| Melded Patterns | 203747 | 21 | `79545187`, `79545198` | 2 |
| Melded Patterns | 203747 | 22 | `795451BA` | 1 |
| Melded Patterns | 203747 | 23 | `7954508E` | 1 |
| Melded Patterns | 203747 | 25 | `795451DD` | 1 |
| Molested Molecules | 203746 | 23 | `79545139` | 1 |
| Molested Molecules | 203746 | 24 | `795451D2`, `795451D7` | 2 |
| Redundant Scan | 204178 | 20 | `7953AF85` | 1 |
| Stim Fiend | 203739 | 9 | `7957E415` | 1 |
| Stim Fiend | 203739 | 12 | `7953AD68`, `79545069`, `79545072`, `7957E128` | 4 |
| Stim Fiend | 203739 | 14 | `7953ABBF` | 1 |
| Stim Fiend | 203739 | 17 | `7953ABAD` | 1 |
| Workman Striker | 203854 | 14 | `7953AFF9`, `7954501A` | 2 |
| Workman Striker | 203854 | 16 | `79545219` | 1 |
| **Total** |  |  |  | **53** |

The fixed scope contains no Filth Flea or Violent Vagabond actor.

## Ranked Cohorts

| Rank | Family | Actors | Existing evidence result |
| ---: | --- | ---: | --- |
| 1 | Disobedient Bot | 12 | **Superseded 2026-07-27:** L8 supplies the exact categorical stream; five captured levels prove the bounded mathematical SAW setup now used by all 12 active actors. |
| 2 | Looter | 7 | Complete L9/L10 equipped-weapon streams exist. Runtime source weapon QL was incorrectly treated as captured contract identity instead of a production-selected value. |
| 3 | Melded Patterns | 7 | L22/L23 have no exact compatible generated profile; remaining captured-level rows include incompatible stable weapon QL observations. |
| 4 | Stim Fiend | 7 | **Superseded 2026-07-27:** exact L10..L14 SAW observations prove the bounded L10..L17 mathematical setup; six actors are restored and only L9 remains fail-closed. |
| 5 | Fragmented Soul | 6 | Compatible-looking rows remain ambiguous between multiple exact generated streams; L19 has no exact profile. |
| 6 | Incomplete Rebuild | 6 | L17 has no exact profile; L18/L19 still have unresolved multi-stream ambiguity. |
| 7 | Workman Striker | 3 | Multiple generated profiles remain semantically ambiguous for these source variants. |
| 8 | Molested Molecules | 3 | No exact L23/L24 generated profiles exist. |
| 9 | Bloodcreeper | 1 | The generated specialized sequence does not reproduce every selected raw stream. |
| 10 | Redundant Scan | 1 | Multiple generated profiles remain semantically ambiguous. |

Looter was the largest cohort with complete compatible generated packet
semantics and only a production-owned value blocking reuse.

## Looter Capture-Backed Archetypes

The seven selected actors use the existing Looter L9 and L10 profiles:

| Level | Generated profile | Captured QL | Capture sessions |
| ---: | --- | ---: | --- |
| 9 | `1f9bcd8f10a573fe-18e6692741ae1557` | 10 | `20260709-210452` |
| 9 | `1f9bcd8f10a573fe-3a02a8bc94c80061` | 8 | `20260708-143600` |
| 10 | `8862442ad0440f58-29d7128dd3295e3e` | 12 | `20260708-143600` |
| 10 | `8862442ad0440f58-6e2dc55a960bb28c` | 8 | `20260708-143600` |
| 10 | `8862442ad0440f58-b2b0641a63fcbe7b` | 11 | `20260708-143600` |
| 10 | `8862442ad0440f58-de5fe0fa20d6a3d1` | 9 | `20260709-210452` |

Representative complete raw chains include:

- L9 QL10: WIFU `20260709-210452|IN|4237|fb2f43b75519`,
  `SpecialAttackWeapon` `|7489|8d494a183f7d`, `Attack`
  `|7490|f8cb1585ddb8`, and `AttackInfo`
  `|7500|67efb8191311`.
- L9 QL8: WIFU `20260708-143600|IN|9602|c5ba8d799e28`,
  `SpecialAttackWeapon` `|15121|c5420c465e85`, `Attack`
  `|15122|601148430e21`, and `AttackInfo`
  `|15154|5ae4750e765c`.
- L10 QL12: WIFU `20260708-143600|IN|11430|eb2c07946571`,
  `SpecialAttackWeapon` `|18074|3c9a7900bb17`, `Attack`
  `|18075|91c9eff9b545`, and `AttackInfo`
  `|18116|4dea52dc693a`.

The exact reusable semantics remain:

- equipped weapon templates `123038/123039`;
- WIFU slot `6`, state-machine id `1000015`, instance `0`, unknown fields
  `11/262`;
- one ordered `WIFU -> SpecialAttackWeapon -> Attack -> AttackInfo` stream;
- `Attack` action `0` and N3 unknown `0`;
- `AttackInfo` slot `6`, damage-type wire `0`, hit-type wire `3`, weapon
  instance `0`, and N3 unknown `0`;
- L9 invariant SAW fields `49/49/45/49`;
- L10 invariant SAW fields `54/54/49/54`;
- finite per-actor energy/ammunition and ordered mutable SAW field 5.

The runtime source rows already own the selected QLs: QL9 for
`795313CB/79545029/7957E5CD`; QL12 for `795312DC/79545034`; QL11 for
`7954503C`; and QL8 for `79557CB8`. The generated stream owns the packet
semantics while the existing source-specific weapon tuple owns the runtime QL.

## Production Repair

`CapturedSubwayCombatCatalog.ForSourceSpecificWeaponArchetype` already required
exactly one source-owned weapon tuple and failed closed for zero or multiple
tuples. Its returned contract did not carry
`UsesProductionWeaponQuality`, causing the catalog to compare the source-owned
QL as immutable capture identity.

The production contract now calls `WithProductionWeaponQuality()` after
building the exact source-specific equipped weapon. This removes only QL from
reusable contract identity. Weapon templates, mode, slot, packet order, level
semantics, hit/damage wires, SAW shape, stream count, and ambiguity checks
remain exact and fail-closed.

The Looter packet regression changes production QL in the WIFU and proves the
captured SAW, Attack, and AttackInfo bodies remain byte-exact. A cross-weapon
case using `122905/122906` remains rejected.

## Result

All seven selected Looter actors now resolve their exact L9/L10 generated
archetype through the shared capture-backed combat path. The pre-existing
certified Looter `7954501B` remains certified, so the full active Looter family
is now `8/8`.

The fixed 53-actor scope is now `7` certified and `46` quarantined. Combined
PF127/PF1931 coverage is `332` certified and `157` quarantined of `489`;
PF127 is `245/77` and PF1931 remains `87/80`.

The 46 remaining actors stay fail-closed for the explicit missing,
incompatible, or ambiguous stream reasons in the ranked table. None was left
quarantined because Looter binding failed.

The approved Subway contract generator completed with `40` archetypes and
produced no generated diff. The narrow active-coverage generator could not
rewrite its checked-in artifact because current repository parsing does not
resolve `NascenceLifeContentModule.JobeResearchPlayfieldId`; the checked-in
active-coverage artifact therefore remains unchanged, and the focused runtime
coverage tests are the authoritative result for this pass.

## 2026-07-27 Next Ranked Slice

Rank 1 Disobedient Bot remains blocked: the only complete reusable generated
archetype is L8, while the twelve fixed-scope actors are L6/L7/L9/L10. Rank 2
Looter is complete and excluded. Rank 3 Melded Patterns is therefore the next
family evaluated.

Only fixed-scope source `0x795451DD` is safely restorable. It is L25,
MonsterData `203747`, and exactly matches generated profile
`41ec2f5fb41b8e2f-5f0a16ad1c7c6589` with equipped templates
`121817/121818`, slot `6`, instance `0`, hit wire `3`, damage wire `0`, and
ordered mutable SAW field 5 values `0 -> 85`. Previously certified L25 source
`0x795451D8` remains certified through the same archetype.

The other fixed-scope Melded actors remain fail-closed:

- L18 `0x79545190` has an independently captured `121818/121818` weapon tuple,
  not the selected `121817/121818` family.
- L20 `0x79545196` has an incompatible stable weapon tuple.
- L21 `0x79545187` and `0x79545198` have no exact compatible selected-family
  profile.
- L22 `0x795451BA` and L23 `0x7954508E` have no canonical raw combat profile.

The fixed 53-actor scope is now `8` certified and `45` quarantined. PF127 is
`246/76`; PF1931 remains `87/80`; combined coverage is `333/156` of `489`.
The active Melded Patterns family moved from `3/7` to `4/6`. The active-coverage
generator now understands the current qualified Nascence constants and current
dynamic/Arete runtime sources; its checked-in artifact regenerates
deterministically at `1,512` audited actors. Full evidence is in
`docs/evidence/SUBWAY_MELDED_PATTERNS_COMBAT_RESTORATION_20260727.md`.

## 2026-07-27 Mathematical Disobedient Bot Restoration

The former Disobedient Bot blocker is resolved without another capture.
Programmatic exact-integer analysis of raw L5/L6/L8/L9/L10 SAW packets proves
fields 1-4 are:

`floor((19 * actorLevel + 28) / 4)`

The bounded results are L5=`30`, L6=`35`, L7=`40`, L8=`45`, L9=`49`, and
L10=`54`. All five leave-one-out evaluations predict the removed captured
point exactly. The rule is restricted to MonsterData `17649`, levels `5..10`,
and exact SIW1 templates `144742/144743`, tag/instance `0x53495731`, natural
specialized mode, slot `0`, numeric hit type `3`, numeric damage type `0`, and
the capture-backed SAW/Attack/AttackInfo stream. It fails closed outside that
domain.

All 12 fixed-scope L6/L7/L9/L10 actors now resolve through generated profile
`ff1685d6a9c45e2c-370328526bcb32c7`. No other remaining family shares the
proved family-specific domain, so the same generator restores zero additional
actors. The fixed 53-actor scope is now `20` certified and `33` quarantined.
PF127 is `258/64`; PF1931 remains `87/80`; combined coverage is `345/144` of
`489`.

Full evidence and the machine-readable PF127/PF1931 dataset are in
`docs/evidence/ENEMY_COMBAT_SETUP_FORMULA_20260727.md` and
`docs/generated/enemy_combat_setup_formula_dataset.json`.

## 2026-07-27 Mathematical Stim Fiend Restoration

The former Stim Fiend blocker is resolved for its bounded captured domain
without another capture. Exact raw L10/L11/L12/L13/L14 SIW1 packets prove:

```text
SpecialAttackWeapon fields 1-4 = floor((11 * actorLevel - 2) / 2)
```

All five leave-one-out evaluations reproduce the held-out packet value. The
selector is restricted to MonsterData `203739`, PF127, levels `10..17`, exact
SIW1 templates `144742/144743`, tag/instance `0x53495731`, natural-specialized
mode, slot `0`, hit type `3`, damage type `0`, and the captured packet
structure. L12 terminal-only damage-type-4 evidence is retained but cannot
become a repeating attack stream. Production continues to own damage, range,
cadence, health, mutable energy/ammunition, and per-actor mutable SAW state.

Six fixed-scope actors are restored: four at L12, one at L14, and one at L17.
L9 source `0x7957E415` remains fail-closed outside the proven domain. The full
active Stim Fiend family is now `14/1`; the fixed 53-actor scope is `26`
certified and `27` quarantined. PF127 is `264/58`; PF1931 remains `87/80`;
combined coverage is `351/138` of `489`.

Full evidence is in
`docs/evidence/STIM_FIEND_COMBAT_SETUP_FORMULA_20260727.md`, and the shared
machine-readable formula dataset now records both accepted ordinary-enemy
formulas and 26 active mathematical bindings.

## 2026-07-28 Mathematical Melded Patterns Restoration

The remaining six Melded Patterns actors are restored through the bounded
equipped-weapon setup:

```text
base = floor((11 * actorLevel - 2) / 2)
SAW Unknown1/2/3/4 = base / base+28 / base / base
```

Thirteen raw SAW packets at L18/L19/L20/L21/L24/L25 and all six
leave-one-out evaluations are exact. The domain is restricted to PF127,
MonsterData `203747`, L18..L25, equipped mode, slot `6`, instance `0`, one
normal stream, action `0`, hit/damage wires `3/0`, and the owner-selected
`items.dat` interpolation tuples `121817/121818`, `121818/121818`, or
`121819/121820`. Actor QL selects the tuple; runtime identity does not.

The previous exact-observation path incorrectly treated owner-selected QL,
template position, Energy/ammunition, and mutable SAW state as reusable
contract identity. Production now owns those values while the captured WIFU,
SAW, Attack, AttackInfo structure and order remain exact. All ten active
Melded Patterns actors are certified, including the partial-chain L22/L23
sources whose categorical loadouts are owner-linked and whose numeric setup is
mathematically proven.

The fixed 53-actor scope is now `32` certified and `21` quarantined. PF127 is
`270/52`; PF1931 remains `87/80`; combined coverage is `357/132` of `489`.
Full evidence is in
`docs/evidence/SUBWAY_MELDED_PATTERNS_COMBAT_RESTORATION_20260727.md`.
