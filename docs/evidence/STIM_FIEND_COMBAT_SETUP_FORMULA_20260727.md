# Stim Fiend Combat Setup Formula

Date: 2026-07-27

## Result

Stim Fiend MonsterData `203739` now uses the bounded combat-setup formula
`stim-fiend-siw1-floor-11L-minus-2-over-2-v1`:

```text
SpecialAttackWeapon fields 1-4 = floor((11 * actorLevel - 2) / 2)
```

The formula is selected only for the exact Stim Fiend natural-specialized
archetype in PF127, levels `10..17`, SIW1 templates `144742/144743`,
tag/instance `0x53495731`, slot `0`, numeric hit type `3`, numeric damage type
`0`, and the captured `SpecialAttackWeapon -> Attack -> AttackInfo` structure.
It does not use source identity, nearest-level selection, interpolation, or a
cross-enemy fallback.

## Active actor inventory

All active rows come from
`CapturedSubwayOrdinaryContentProvider.cs`, use runtime selector
`subway.ordinary.stim_fiend`, PF127, and MonsterData `203739`.

| Level | Runtime source | Binding key | Coverage key | Current state |
| ---: | --- | --- | --- | --- |
| 9 | `0x7957E415` | `f66ea5ef121cb4ef9dec` | `84e7987220013f9e0d21` | Quarantined: outside proven formula domain |
| 10 | `0x7953AD66` | `b0e9808aea0916cbda30` | `3a12164df21619cca53b` | Certified mathematical setup |
| 10 | `0x7957E5CF` | `43d1427c3026c781653e` | `6be87934bedc30564a19` | Certified mathematical setup |
| 10 | `0x7957E5D0` | `1f03999d1e04a355183e` | `37e8ee37d3c19c97eb2c` | Certified mathematical setup |
| 10 | `0x7957E5D1` | `651b00acecad762b0047` | `3d1c0452f11022d7fc8a` | Certified mathematical setup |
| 11 | `0x79557F12` | `92537f6888d38ab34c2d` | `1cef20c998450476f1fa` | Certified mathematical setup |
| 12 | `0x7953AD68` | `898d24c9fc99fccb42f5` | `26e744a2b100cdbaf80e` | Certified mathematical setup |
| 12 | `0x79545069` | `c0c8fe6afc105bd53b0b` | `c34a66eae0e77786741e` | Certified mathematical setup |
| 12 | `0x79545072` | `07206fb734f380e844a4` | `d462074facc2a9e76ba9` | Certified mathematical setup |
| 12 | `0x7957E128` | `72d471568a8c059e7eff` | `cee2fd5f19f42fafaa52` | Certified mathematical setup |
| 13 | `0x7953AA4B` | `3a8edcf736f4e91c62f9` | `a78fe96bd75c85138d88` | Certified mathematical setup |
| 13 | `0x7953AD7D` | `ce9086512411db1d178f` | `b809006c031500eba894` | Certified mathematical setup |
| 13 | `0x795451F5` | `22acaa8c7ffa456af7ee` | `c0358e414447731394f8` | Certified mathematical setup |
| 14 | `0x7953ABBF` | `8507c659041ce08c3793` | `1d10691a9ab3459d9d74` | Certified mathematical setup |
| 17 | `0x7953ABAD` | `521392679ed324ea3014` | `247a87f50d21866fe322` | Certified mathematical setup |

The seven-actor starting quarantine scope was L9 `0x7957E415`; L12
`0x7953AD68`, `0x79545069`, `0x79545072`, `0x7957E128`; L14 `0x7953ABBF`;
and L17 `0x7953ABAD`. These are exactly seven unique binding and coverage rows.

## Raw packet observations

| Level | Capture | Raw ordinal | Source | SAW fields 1-4 | Formula |
| ---: | --- | ---: | --- | ---: | ---: |
| 10 | `20260708-143600` | 17386 | `0x794CD773` | 54 | 54 |
| 11 | `20260708-143600` | 17877 | `0x794CD77C` | 59 | 59 |
| 12 | `20260708-143600` | 18584 | `0x794CD778` | 65 | 65 |
| 13 | `20260709-212115` | 12882 | `0x7953AA4B` | 70 | 70 |
| 14 | `20260709-220439` | 7612 | `0x7953ABAF` | 76 | 76 |

All five raw packets use SIW1 templates `144742/144743`, instance
`0x53495731`, and mutable field 5 value `0`. The generated values through the
bounded active domain are:

| Level | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SAW fields 1-4 | 54 | 59 | 65 | 70 | 76 | 81 | 87 | 92 |

Leave-one-out validation predicts each removed L10/L11/L12/L13/L14 raw value
exactly (`5/5`). The exact raw SAW bodies for all five observations and the
generated L17 packet are covered by byte-level tests.

The natural-specialized observations contain no WIFU packet. Their decisive
chain is inbound `SpecialAttackWeapon -> Attack -> AttackInfo`; the generated
dataset records timestamp, direction, source, target, SCFU generation key,
SAW/Attack/AttackInfo packet IDs, the absence of WIFU, action `0`, stream
ordinal, hit and damage wires, ammo observations, damage observations, first
hit/interval observations, terminal classification, and exact SAW body. Energy
and weapon QL are not present because SIW1 is the natural-specialized context,
not an equipped item. StopFight, action-99, and health transitions remain
lifecycle/terminal evidence and do not participate in numeric formula
selection.

## Formula families evaluated

The existing exact affine search evaluated `196,452` integer/rational
numerator, intercept, denominator, and floor/ceiling/nearest candidates against
the five captured points. The analysis also evaluated bounded piecewise and
breakpoint domains, finite differences, stream-specific selection, direct item
stat transformations, integer clamps, an unbounded extension, the existing
Disobedient Bot expression, and generic four-equal SIW1 cross-family reuse.

Multiple affine expressions can fit five points. The selected expression is
the simplest exact positive-integer floor form supported by the standard SIW1
held-out observations, and is still bounded by the exact Stim Fiend semantic
selector and L10..L17 domain. There is no clamp.

## Captured semantic archetypes

The exact generated semantic profiles are:

- L10 `5aa2541e7645c589-9bcb7a58208cf1e0`
- L11 `8dc794414961f6e6-63cd3e499be4e58b`
- L12 `963ecf2aa60f045c-de110ebeb7e358cd`
- L13 `3f70ab044f0e78d5-d2b65cf5c70d61d6`
- L14 `54d40b70fa1a801a-064305180fc7f1ad`

The L12 profile contains one normal repeating stream and one terminal-only
damage-type-4 result. Runtime selection retains that terminal evidence but
excludes it from the reusable repeating attack stream. The previous generic
natural-archetype gate incorrectly required `Streams.Length == 1`, so the
otherwise compatible L12 profile was rejected. It now requires exactly one
non-terminal reusable natural stream and preserves all terminal evidence.

L14 has capture-safe categorical semantics but no landed interval observation.
Runtime cadence therefore remains owned by the existing Stim Fiend production
archetype rather than treating a first-observed delay as steady-state cadence.

Production continues to own the active spawn level, `10..16` damage range,
`5.666535` recharge cadence, attack range, health and derived stats, mutable
energy/ammunition, and per-actor mutable SAW state. No new gameplay formula was
added for those values.

Only SAW numeric fields 1-4 moved to generated ownership. MonsterData, SIW1
templates/name/tag, natural mode, stream count/order, slot, instance, action,
hit type, damage type, terminal distinction, and packet structures remain
capture-bound.

## Rejected alternatives

- The unrounded line `5.5 * level - 1` is not an integer rule at L11 and L13.
- The Disobedient Bot formula mismatches three of five Stim observations.
- Item-template interpolation mismatches all five observed SAW values.
- Applying the numeric expression to every four-equal SIW1 observation
  mismatches `6/23` held-out cross-family observations. The exact Stim Fiend
  family and stream selector is therefore mandatory.
- Levels below `10` and above `17` remain unsupported. In particular, active L9
  source `0x7957E415` remains fail-closed.

## Active result

The formula is available to all 14 active PF127 Stim Fiends in its proven
L10..L17 domain. In the fixed seven-actor restoration scope, six actors are
restored:

- L12: `0x7953AD68`, `0x79545069`, `0x79545072`, `0x7957E128`
- L14: `0x7953ABBF`
- L17: `0x7953ABAD`

L9 source `0x7957E415` remains quarantined because it is outside the proven
Stim Fiend domain. The full active family is now `14` certified and `1`
quarantined. PF127/PF1931 fixed active coverage moves from `345/144` to
`351/138` of `489`; PF127 is `264/58`, PF1931 remains `87/80`, and the fixed
53-actor scope is `26/27`.

The machine-readable observations, leave-one-out results, rejected candidates,
cross-family reconciliation, active bindings, and starting-scope outcome are
in `docs/generated/enemy_combat_setup_formula_dataset.json`. That file is
`8,502,381` bytes and contains `422`
PF127/PF1931 compact profiles, `42` referenced item templates, two accepted
formula records, `26` active mathematical bindings, five Stim raw chain
observations, five Stim leave-one-out rows, and 23 cross-family held-out rows.
