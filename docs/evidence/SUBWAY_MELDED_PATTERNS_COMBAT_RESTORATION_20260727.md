# Subway Melded Patterns Combat Restoration

Date: 2026-07-27

## Selection

Melded Patterns is rank 3 in the fixed remaining Subway cohort audit. Rank 1
Disobedient Bot remains blocked because its twelve active L6/L7/L9/L10 actors
cannot use the only complete reusable L8 generated archetype. Rank 2 Looter was
completed in the accepted baseline commit and is excluded.

The safely restorable rank-3 subset is one actor:

| Runtime source | Family | MonsterData | Level | Before | After |
| --- | --- | ---: | ---: | --- | --- |
| `0x795451DD` | Melded Patterns | 203747 | 25 | quarantined | certified |

Previously certified L25 source `0x795451D8` is an explicit no-regression
member of the same runtime archetype.

## Exact Capture Correlation

The selected runtime actor uses generated semantic profile
`41ec2f5fb41b8e2f-5f0a16ad1c7c6589`. The profile is source-correlated to
`0x795451DD` and uses captures `20260709-222339` and `20260709-225408`.

Decisive packet chains:

- `20260709-222339`: WIFU
  `IN|11652|38ef58bba40e`, `SpecialAttackWeapon`
  `IN|12674|e5cb9935961f`, `Attack`
  `IN|12675|f8ec554edeaa`, and normal `AttackInfo`
  `IN|12777|42c67b98aeb2` against target `0x7944C065`.
- `20260709-225408`: WIFU
  `IN|14817|adde67512877`, `SpecialAttackWeapon`
  `IN|15077|0e510533da86`, `Attack`
  `IN|15078|71f716333a3f`, and normal `AttackInfo`
  `IN|15641|0ec176e6c849` / `IN|15766|f238fa6fe8b4` against the same target.
- The first chain lands for 25 damage with ammo 19. The later chain lands for
  34 and 27 damage with ammo 0. These are actor-owned normal results, not
  observer or player-owned packets.
- Captured StopFight rows 11981 and 11987 terminate the fight. They are
  cancellation boundaries, not extra attack streams.
- The `20260709-225408` target-death boundary occurs at
  `2026-07-10T04:04:33.622Z`; the source-linked corpse update follows at
  `04:04:33.890Z`. No NPC-owned critical, miss, or action-99 terminal result is
  present for this profile, so none is synthesized.

The capture proves one normal stream. Player-owned attacks, incomplete
prefixes, interleaved attackers, and target-health-only changes were excluded
by the existing attribution rules.

## Capture-Bound Contract

The selected stream retains:

- equipped weapon templates `121817/121818`;
- WIFU inventory slot `6`, state-machine type `1000015`, instance `0`, and
  captured structure;
- `SpecialAttackWeapon` N3 unknown `0`, invariant fields
  `136/164/136/136`, no special list, and ordered mutable field 5 observations
  `0 -> 85`;
- `Attack` N3 unknown `0` and action `0`;
- `AttackInfo` slot `6`, weapon instance `0`, unknown `0`, hit-type wire `3`,
  damage-type wire `0`, and N3 unknown `0`;
- exactly one `WIFU -> SpecialAttackWeapon -> Attack -> AttackInfo` stream.

Captured first-hit delays are `2.984133` and `18.044752` seconds; the landed
interval is `4.378445` seconds and the captured item cycle is `4.7` seconds.
Those observations remain evidence. Existing item/combat and
`NpcCombatTickCoordinator` owners control runtime phase, cadence, damage,
range, and cancellation.

## Production-Owned State

The active spawn contract selects QL20 through the existing
`NpcCombatAttackRules.CapturedSubwayMeldedPatternsWeaponQuality` owner.
Generated WIFU evidence contains QL19, Energy values 20 and 7, and
MultipleCount 1. Existing production weapon state owns runtime QL, Energy, and
ammunition; the generator now emits the complete two-observation SAW replay
without treating those production-owned values as semantic identity.

The extractor extension is conservative: it accepts only a two-state equipped
SAW replay when the remaining blockers are exactly the already-owned ammo and
WIFU Energy/MultipleCount state. Longer or semantically unsupported sequences
remain fail-closed. A complete full-corpus write followed by a second full
`--check` produced no diff.

## Incompatible Family Members

| Source | Level | Result |
| --- | ---: | --- |
| `0x79545190` | 18 | Quarantined; its exact captured WIFU is `121818/121818`, not the selected weapon family. |
| `0x79545196` | 20 | Quarantined; stable weapon tuple is incompatible. |
| `0x79545187`, `0x79545198` | 21 | Quarantined; no exact compatible selected-family profile. |
| `0x795451BA` | 22 | Quarantined; no canonical raw combat profile. |
| `0x7954508E` | 23 | Quarantined; no canonical raw combat profile. |

L19 source `0x79545185` remains certified through its distinct exact
`121819/121820` QL22 generated profile
`67f518afac8fd529-88660aa55a7b2d5c`; it is not merged with the restored L25
archetype.

## Production and Validation Result

`OrdinaryEnemyCatalog` marks production QL only for the exact L25 Melded
contract. The generated catalog adds the ordered `0,85` SAW state for profile
`41ec2f5fb41b8e2f-5f0a16ad1c7c6589`. Focused catalog tests enumerate both L25
actors exactly once, identify `0x795451DD` as the one newly restored actor,
retain `0x795451D8`, preserve the distinct L19 weapon profile, and reject every
unsupported level or weapon family. The packet regression proves exact
WIFU/SAW/Attack/AttackInfo ordering and byte identity with only production QL
changed in WIFU.

Counts:

- PF127: `245/77` -> `246/76`.
- PF1931: unchanged `87/80`.
- Combined: `332/157` -> `333/156`, denominator `489`.
- Active Melded Patterns: `3/7` -> `4/6`.
- Fixed 53-actor scope: `7/46` -> `8/45`.
- Runtime rejection rows: `156`, matching `156` unique quarantined actors.

Validation:

- Extractor self-test: PASS.
- Full-corpus generation: PASS at `364` sessions, `2,647` complete chains,
  `243` certified profiles, `100` runtime-ready definitions, and `0` errors;
  the monitored second `--check` completed with no generated diff.
- Active-coverage generator/check: PASS at `1,512` audited actors.
- Melded Patterns focused resolution and packet tests: `4/4` PASS.
- Combat catalog: `47/47` PASS; packet factory: `37/37` PASS; generated
  exact-byte fixtures: `3/3` PASS.
- Attribution: `1/1` PASS; miss/normal/critical/terminal Filth Flea regression:
  `7/7` PASS; collision: `17/17` PASS; chase/range/cancellation: `38/38` PASS;
  Temple: `10/10` PASS; active coverage: `3/3` PASS.
- Starting-SHA accepted failures remain unchanged: Subway `65/68`, Abmouth
  `23/26`, and world population `35/39`.
- Complete messaging suite: `520/559` PASS with `39` accepted unrelated
  failures. The starting-SHA baseline was `514/556` with `42` failures; this
  slice adds no failure and repairs the three stale active-coverage failures.
- Debug build: PASS after the documented engine-stop lock recovery.
- Engine restart: PASS; ports `6996`, `7012`, `7500`, and `7501` are listening.
