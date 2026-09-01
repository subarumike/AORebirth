# Sector 10 Capture Baseline

## Status and authority

This document preserves the capture-backed baseline for Sector 10 so the playfields can be implemented later without re-deriving the completed work.

- Outer Sector 10: playfield resource `4374`
- Team instance: playfield resource `4474`
- Solo instance: playfield resource `4475`
- Captures are authoritative for observed identities, packets, loot, damage, spawn behavior, and mechanics.
- Mike's explicit gameplay decisions in this document are implementation authority where they refine packet timing or visible behavior.
- Combat duration is intentionally excluded because the capturing character was extremely overpowered.
- Do not replace observed ranges with uncaptured formulas. Preserve unresolved fields until new evidence closes them.

## Evidence corpus

Two completed-capture roots were reconciled:

- `C:\Users\Mike\Documents\AORebirth\Captures`
- `D:\AOTools\ReadyToUse\Plugins\captures`

Corpus totals:

- 52 Sector 10 capture folders
- 692 decoded SCFU rows
- 0 SCFU decode errors
- 42 positive boss-corpse loot snapshots
- 31 solo-instance boss loot snapshots
- 11 team-instance boss loot snapshots
- 0 inventory decode errors

Capture folders with no combat, no SCFU, or no positive corpse inventory were not counted as kills. They were retained and used for the evidence they actually contain, including loot-only, idle, distance, visibility, and aggro tests. Outer-playfield ordinary aliens were not mixed into the instance-boss damage or loot denominators.

### Outer playfield captures

- `20260830-034520`
- `20260830-035031`

### Team-instance captures

- `20260830-034033`
- `20260830-040432`
- `20260830-040731`
- `20260830-041457`
- `20260830-041643`
- `20260830-041816`
- `20260830-041950`
- `20260830-042105`
- `20260830-042257`
- `20260830-042449`
- `20260830-042700`
- `20260830-042926`
- `20260830-043059`

### Solo-instance captures

- `20260830-220829`
- `20260830-221207`
- `20260830-221519`
- `20260830-221903`
- `20260830-221929`
- `20260830-222041`
- `20260830-222152`
- `20260830-222611`
- `20260831-013216`
- `20260831-020346`
- `20260831-044555`
- `20260831-045427`
- `20260831-045756`
- `20260831-234330`
- `20260901-001419`
- `20260901-010725`
- `20260901-011309`
- `20260901-011713`
- `20260901-012212`
- `20260901-012357`
- `20260901-012925`
- `20260901-013445`
- `20260901-014431`
- `20260901-020657`
- `20260901-021044`
- `20260901-021355`
- `20260901-021605`
- `20260901-021720`
- `20260901-021846`
- `20260901-022324`
- `20260901-022506`
- `20260901-022619`
- `20260901-023331`
- `20260901-024002`
- `20260901-024430`
- `20260901-024528`
- `20260901-024648`

## Shared Khazoh Ra boss identity and stats

The captured boss variants are:

- Ilari Khazoh Ra
- Cha Khazoh Ra
- Chemist Khazoh Ra
- Ankari Khazoh Ra

Shared captured stats:

| Field | Baseline |
|---|---:|
| Level | 190 |
| Health | 289,975 |
| MonsterData | 257313 |
| Monster scale | 168 |
| Run speed | 1,459 |
| Corpse credits | 35,507 |
| Innate attack slots | 3 |

The credit value was present in all 14 identity-linked corpse snapshots. One Cha snapshot exposed run speed `742`; treat that as a captured state variation, not the spawn baseline, because all other boss SCFUs use `1459`.

## Relay spawn mechanic

Resolved item-to-boss mappings:

| Used item | Item ID | Spawned boss | Resolved samples |
|---|---:|---|---:|
| Alpha Signal Relay | 284291 | Cha Khazoh Ra | 4 |
| Sigma Signal Relay | 284292 | Chemist Khazoh Ra | 5 |
| Kappa Signal Relay | 284294 | Ankari Khazoh Ra | 4 |

`Rho Signal Relay` (`284293`) is the remaining named relay and Ilari is the remaining boss variant, but the older Ilari captures do not contain a resolved live item-use observation. Keep `Rho -> Ilari` unpromoted until an exact item-use bridge or explicit implementation decision is recorded.

Across 13 resolved relay uses:

- Use request to boss SCFU: `2.039-2.280 seconds`
- Mean use-to-SCFU delay: `2.092 seconds`
- Implementation baseline: approximately `2 seconds`
- The consumed relay receives a correlated delete at spawn completion.
- Multiple precise samples place the first boss appearance about `2 metres` from the player.
- The boss then teleports to the arena point at approximately `X=230, Z=190`.
- SCFU-to-arena-position transition: `0.293-0.720 seconds` in resolved samples.
- Initial arena Y was approximately `13.67-14.93`; the boss settles near `Y=10.685`.

This proves the visible mechanic Mike observed: the boss first appears on or immediately beside the player, then teleports to the arena spawn area.

## Aggro, facing, and visibility

### Final implementation baseline selected by Mike

- Forward vision cone: `180 degrees total`
- Forward half-angle: `90 degrees left/right from center`
- Forward vision range: `40 metres`
- Rear/all-direction proximity circle: `15 metres`
- The forward cone applies where the player is inside its arc; the proximity circle covers rear and side approaches.

### Captured support

- Reliable far aggro occurred as far as `39.63 metres`.
- Far aggro was observed at `75.4 degrees` off center at `39.63 metres`.
- Far aggro was observed at `82.3 degrees` off center at `34.63 metres`.
- Those observations rule out a 120-degree total cone as the complete baseline.
- Direct-rear packet observations occurred at `16.64 metres / 176.2 degrees` and `19.65 metres / 173.4 degrees`.
- An earlier controlled rear approach triggered at `14.69 metres` before heading capture was added.
- Mike selected a 15-metre rear circle; larger rear packet distances are treated as acquisition/attack packet transition delay rather than the configured radius.
- Two events at `96.9` and `109.5 degrees` occurred at `24.82` and `29.07 metres`. Do not widen the selected cone from these alone because heading changes and packet timing were not isolated.

### Visibility boundary

- Live-boss appear/disappear tests cluster around approximately `65-70 metres`.
- Mike confirmed this behavior is distance-based, not obstruction-based line of sight in the open arena.
- Visibility/in-play range is distinct from both the 40-metre forward aggro cone and the 15-metre rear proximity circle.

## Observed combat damage

These are final captured `AttackInfo` amounts against Mike's capturing character. They are not unmitigated NPC weapon-template values. Do not promote them as exact weapon formulas without a bridge from mitigation and attack type.

| Source | Hits | Captures | Minimum | Median | Maximum |
|---|---:|---:|---:|---:|---:|
| Ilari Khazoh Ra | 89 | 9 | 208 | 1,969 | 4,285 |
| Cha Khazoh Ra | 64 | 10 | 223 | 2,282 | 4,994 |
| Chemist Khazoh Ra | 43 | 9 | 258 | 1,385 | 3,536 |
| Ankari Khazoh Ra | 69 | 8 | 209 | 1,603 | 2,271 |
| Alien Larvae | 271 | 6 | 226 | 1,010 | 2,165 |

Combat duration, attack-count-per-fight, and time-to-kill are not implementation baselines because the capturing character killed the bosses much faster than an ordinary level-appropriate character.

## Boss-specific mechanics

### Ilari Khazoh Ra

- Three direct innate attack families are captured.
- No separate summoned actor is proven in the solo Ilari captures.

### Cha Khazoh Ra

- Summons `Alien Defense System` actors.
- Summons `Automated Defense System` actors.
- Some fights also produced `Alien Larvae`.
- Alien Larvae have direct captured attacks.
- Captured summon totals are lower bounds and vary with the artificially short fights; do not use those totals as configured wave counts.

### Chemist Khazoh Ra

- Spawns `Alien Napalm` actors during combat.
- Between 1 and 4 unique Alien Napalm actors were observed in each captured fight.
- Exact total, interval, lifetime, and damage application remain duration-sensitive and are not yet exact implementation values.

### Ankari Khazoh Ra

- No summoned actor was observed in the Ankari captures.
- Applies a nano drain during combat.
- Final baseline selected by Mike: a flat `2% of current nano` per drain application.
- Do not hardcode the observed point losses; they are results of the percentage mechanic.

## Shared boss loot table

The same loot structure was observed in the solo and team instances, so all 42 complete boss-corpse snapshots are combined. Loot is arena-wide/shared and is not boss-variant-specific.

### Guaranteed rows

| Loot | Quantity | Observations | Probability |
|---|---:|---:|---:|
| Mercury-Based Temperature Gauge, ID 287147, QL 200 | 1 | 42/42 | 100% |
| Hacker ICE-Breaker Source, ID 257968, QL 1 | 2 | 42/42 | 100% |

### Inactive-component quantity roll

| Component quantity | Corpses | Observed probability |
|---:|---:|---:|
| 3 | 19/42 | 45.2% |
| 4 | 23/42 | 54.8% |

The observed distribution is consistent with an intended 50/50 roll between three and four components. Component selections allow duplicates within one corpse.

### Individual inactive-component observations

| Component | Item ID | Corpses | Units | Observed corpse probability |
|---|---:|---:|---:|---:|
| Inactive Alien Translation Device | 268477 | 16 | 16 | 38.1% |
| Inactive Empty Alien Augmentation Device | 268493 | 18 | 20 | 42.9% |
| Inactive Alien Beacon | 268494 | 13 | 17 | 31.0% |
| Inactive Alien Battery | 268496 | 24 | 28 | 57.1% |
| Inactive Alien Reflex Modifier | 268499 | 19 | 22 | 45.2% |
| Inactive Alien Tank Armor | 268507 | 23 | 23 | 54.8% |
| Inactive Alien Material Conversion Kit | 268510 | 20 | 23 | 47.6% |

All captured inactive components were QL 150.

### Rare-slot roll

| Result | Corpses | Observed probability |
|---|---:|---:|
| No rare item | 21/42 | 50.0% |
| One rare item | 21/42 | 50.0% |

The captured evidence supports one 50% rare-slot roll. Exactly one rare item appears when that slot succeeds.

| Rare result | Item ID | Drops | Overall probability | Captured QL range |
|---|---:|---:|---:|---:|
| Enduring Lead Viralbots | 247136/247137 | 5 | 11.9% | 157-237 |
| Strong Lead Viralbots | 247138/247139 | 4 | 9.5% | 180-236 |
| Supple Lead Viralbots | 247140/247141 | 2 | 4.8% | 162-222 |
| Observant Lead Viralbots | 247142/247143 | 3 | 7.1% | 153-201 |
| Arithmetic Lead Viralbots | 247144/247145 | 4 | 9.5% | 156-200 |
| Untuned Kyr'ozch Signal Relay | 284290 | 3 | 7.1% | 200 |

The six outcomes total exactly 21 successful rare slots. An equal one-of-six selection after the 50% rare roll would yield an intended overall probability of approximately `8.33%` per rare result, but the corpus proves only the observed counts above; equal weighting remains a candidate implementation rule until explicitly accepted.

## Implementation constraints for future PF 4474/PF 4475 work

- Use the selected `180-degree / 40-metre` forward cone and `15-metre` proximity circle.
- Keep client visibility range separate from aggro.
- Reproduce relay consumption, near-player initial spawn, and teleport to the arena position.
- Keep arena loot shared across boss variants.
- Use one guaranteed gauge, two guaranteed ICE-Breaker Sources, a three-or-four inactive-component roll, and a 50% single rare slot.
- Implement Ankari's nano drain as 2% of current nano, not a captured fixed point amount.
- Do not derive attack cadence, summon totals, or combat duration from these fast kills.
- Do not promote observed final damage amounts as raw weapon-template damage.
- Preserve Rho-to-Ilari mapping, exact vision-edge behavior, exact summon schedules, and unmitigated attack formulas as unresolved until an exact bridge or explicit implementation decision exists.
