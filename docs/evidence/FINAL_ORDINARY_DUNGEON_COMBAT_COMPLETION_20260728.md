# Final Ordinary Dungeon Combat Completion — 2026-07-28

> **PF1931 status authority (2026-08-01):** Evidence/provenance only. Current PF1931 status is the [Temple acceptance matrix](PF1931_TEMPLE_ACCEPTANCE_MATRIX_20260801.md); any PF1931 completion, blocker, or test-count statement below is superseded by that matrix.

## Result

The fixed ordinary-dungeon denominator is complete:

| Playfield | Before | After |
|---|---:|---:|
| PF127 Subway | 299 certified / 23 quarantined | 322 / 0 |
| PF1931 Temple | 165 certified / 2 quarantined | 167 / 0 |
| Combined | 464 certified / 25 quarantined | 489 / 0 |

Named encounters remain outside this denominator.

## Starting 25 actors and final disposition

All starting rejection rows reconciled one-to-one with unique active actors.

| Family | Level | Source identities | Count | Final |
|---|---:|---|---:|---|
| Violent Vagabond | 6 | `0x7953AD40`, `0x7953AD49`, `0x7957E123`, `0x7957E40E`, `0x7957E5C5` | 5 | certified |
| Violent Vagabond | 7 | `0x7953AD48`, `0x7953AD4A`, `0x7953AD4C`, `0x7953AF49`, `0x7953AFA1`, `0x7957405C`, `0x7957E02C`, `0x7957E02E`, `0x7957E5BF`, `0x7957E5C4` | 10 | certified |
| Violent Vagabond | 8 | `0x7953AD54` | 1 | certified |
| Violent Vagabond | 10 | `0x7953AA4A`, `0x7953AD58`, `0x7953AD76`, `0x79557CAC`, `0x795743A7`, `0x795743A8` | 6 | certified |
| Stim Fiend | 9 | `0x7957E415` | 1 | certified |
| Eternal Sentinel | 18 | `0x7983FA22`, `0x7983FBC2` | 2 | certified |

Runtime identity is recorded for reconciliation only. It is not a formula or
archetype selector.

## Violent Vagabond

### Exact categorical domain

- PF127, MonsterData `203733`, equipped presentation weapon `130590/130590`,
  QL1, slot 6.
- WIFU: N3 `0`, unknown1 `11`, state machine `1000015/0`, unknown2 `262`,
  flags `4199425`, MultipleCount `1`, Energy `1`, AttackDelay/RechargeDelay
  `175/175`.
- Empty `SpecialAttackWeapon`, Attack N3/action `0/0`.
- Result domain `equipped-melee-empty-saw-slot6-normal-result-v1`: normal hit
  wire `3`, damage wire `0`, slot `6`, instance `0`.
- Exact order remains WIFU → SAW → Attack → AttackInfo. Miss chains end in
  MissedAttackInfo N3 `1`, fields `0/6/0`.

### Decisive raw evidence

Capture `20260708-143600` supplies the exact owner-linked setup:

- L6: WIFU IN `#888`; SAW `#11154`; Attack `#11155`; MissedAttackInfo
  `#11209`.
- L7: SAW `#10474`; Attack `#10475`; MissedAttackInfo `#10526`.
- L10: WIFU `#19184`; SAW `#19378`; Attack `#19379`; MissedAttackInfo
  `#19396`.

The corpus contains 41 raw and 40 distinct Vagabond miss chains. Embedded
attacker/defender attribution is retained. The generic result comparison found
166 compatible captured ordinary equipped-melee normal-result streams with
hit/damage `3/0`; finite-ranged damage-wire-4 streams are a separate category
and are excluded.

### Bounded numeric setup

Formula ID: `violent-vagabond-saw-bounded-affine-floor-v1`, valid only for
MonsterData `203733`, weapon `130590/130590` QL1, slot 6, levels 6 through 10.

```text
U1 = floor((17*L + 26) / 4)
U2 = floor((19*L + 26) / 4)
U3 = floor((15*L + 26) / 4)
U4 = floor((17*L + 25) / 4)
```

It reproduces every observation exactly:

| Level | Observed/generated U1/U2/U3/U4 |
|---:|---|
| 6 | `32/35/29/31` |
| 7 | `36/39/32/36` |
| 8 | `40/44/36/40` generated |
| 10 | `49/54/44/48` |

Positive integer division is floor. Checked integer arithmetic is used.
Unknown5 (`0`, `23`, `40` observed) remains ordered mutable actor state.
Leave-one-out validation has zero mismatches.

Rejected alternatives: actor identity, per-level output tables, nearest-level
selection, L7/L10 copying, miss-only combat, Mugger damage substitution,
generic weapons, and an unbounded level domain.

The `130590` item is presentation data rather than a valid combat-stat owner.
Existing actor stats own damage and damage bonus; the shared melee range owner
and captured WIFU cadence remain in force.

## Stim Fiend level 9

Formula ID `stim-fiend-siw1-floor-11L-minus-2-over-2-v1` is now bounded to
levels 9 through 17:

```text
U1 = U2 = U3 = U4 = floor((11*L - 2) / 2)
```

L9 generates `48/48/48/48`. Existing L10..L14 raw observations remain exact
and leave-one-out validation remains zero-mismatch.

The active PF127 MonsterData `203739` generation independently selects the
single SIW1 category: templates `144742/144743`, tag/instance `1397315377`,
slot 0, action 0, hit/damage `3/0`, with the established
SAW → Attack → AttackInfo order. Compatible semantic profiles are:

- `5aa2541e7645c589-9bcb7a58208cf1e0`
- `8dc794414961f6e6-63cd3e499be4e58b`
- `963ecf2aa60f045c-de110ebeb7e358cd`
- `3f70ab044f0e78d5-d2b65cf5c70d61d6`
- `54d40b70fa1a801a-064305180fc7f1ad`

SCFU evidence is `20260710-202132` IN `#1016`. No capture-local attack was
fabricated: the authoritative active-generation selector supplies the exact
categorical contract, and the bounded formula supplies only numeric setup.
Levels below 9 and above 17 fail closed.

## Eternal Sentinel level 18

Formula ID:
`eternal-sentinel-saw-floor-11L-minus-2-over-2-plus-floor-L-plus-4-over-2-v1`,
bounded to MonsterData `41690`, levels 18 through 20, slot 6, and the declared
123381..123384 weapon partitions.

```text
U1 = U2 = U3 = floor((11*L - 2) / 2)
U4 = floor((L + 4) / 2)
```

Both L18 actors generate `98/98/98/11`.

| Source | Active WIFU | QL | Raw L18 chain |
|---|---|---:|---|
| `0x7983FA22` | `123381/123382` | 15 | `20260721-042139` SAW `#233`, Attack `#234`, misses `#261/#316` |
| `0x7983FBC2` | `123383/123384` | 22 | `20260721-042139` SAW `#1350`, Attack `#1351`, miss `#1365` |

Exact active WIFUs are `20260721-041439` IN `#2322` and `#2338`. Both retain
slot 6, state machine `1000015/0`, Energy `-1`, and delay `235/235`.

Complete compatible normal-result references:

- `ba0dc14f053cc59f-71ed92b48bc9d461` (L19)
- `e037cf6f4165eff5-71ebcc342951c27c` (L20)
- `e037cf6f4165eff5-c036f50d1289554a` (L20)

They independently establish the equipped-melee AttackInfo category:
hit/damage `3/0`, slot 6, instance 0, action 0, and exact packet order.
Production owns numeric damage, range, cadence, QL-selected item statistics,
Energy, and mutable SAW state.

## Shared fixed-spawn correction

`OrdinaryEnemySpawnLevelDefinition.Fixed` previously reconstructed a new
variant from numeric level fields and discarded the authoritative fixed
variant's weapon loadout. Fixed definitions now retain and return that exact
variant. Validation requires the retained variant to match the fixed numeric
definition. Inclusive ranges remain generated and explicit observed variants
remain independently selected.

This is what makes the active Eternal WIFU selector reach the existing
source-variant combat resolver. It also preserves all other fixed loadouts
without adding identity fallback matching.

## Production ownership and rejected behavior

Capture remains authoritative for family, MonsterData, weapon/mode, slot,
instance, action, hit/damage wires, stream structure, packet family, result
class, and packet order. Existing production systems continue to own health,
damage, damage bonus, range, cadence, runtime QL, ammunition/Energy, and
mutable ordered state.

No nearest-level selection, MonsterData-only fallback, generic weapon, copied
damage, zero-damage/miss-only loop, actor whitelist, duplicate contract, or
cross-enemy archetype was added.

## Deterministic validation

- Extractor self-test: PASS.
- Formula dataset check: PASS.
- Active-coverage generation/check: PASS; the 25 final rows changed from
  quarantine to certified.
- Focused formula, categorical rejection, runtime resolution, shared packet
  path, fixed-loadout preservation, and 489-actor reconciliation tests: PASS.
- Production ZoneEngine compilation: PASS.
- The repository Visual Studio wrapper remains blocked because
  `vstest.console.exe` is absent. The direct test-project compile and focused
  reflection runner were used for the final suite.
- Full-corpus regeneration was attempted with the approved extractor. Three
  bounded invocations remained active for more than ten minutes at roughly
  1.59 GB each and produced no terminal output; they were terminated. The
  unchanged checked-in 374-session inventory remains the source inventory.
  Narrow deterministic formula and coverage generation passed twice.
