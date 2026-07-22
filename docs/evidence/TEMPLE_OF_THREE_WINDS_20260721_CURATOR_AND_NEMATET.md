# Temple of Three Winds: The Curator and Nematet

## Scope

This record promotes the next PF1931 named-encounter slice from finalized
official-live captures. Earlier captures through `20260721-044256` were already
implemented. Capture `20260721-052115` supplies exact full-update appearance
generations; the two newly promoted fights are:

- `20260721-225404`: The Curator fight, death, corpse, and loot.
- `20260721-225743`: Nematet the Custodian of Time fight, death, corpse, and loot.

PF647 remains the preceding transfer/gateway. PF1931 owns Temple rooms,
population, combat, corpses, and loot.

## Capture integrity

- The analyzer recovered `34` SCFU rows for `225404` and `8` for `225743`, with
  zero failures and zero incomplete rows.
- The lifecycle decoder reported complete SCFU and corpse decoding, zero decode
  errors, `processingAllowed=true`, and no recapture or offline-decode need.

## Identity-first profile evidence

| Encounter | Exact fight identity | New fight generation | Appearance source | Monster data | Runtime anchor |
| --- | --- | --- | --- | --- | --- |
| The Curator | `SimpleChar:79872F43` | L52, 9,740 HP, scale 106 | exact `052115` Curator SCFU | `22802` | `(121.159302, 34.0749969, 352.137634)` |
| Nematet | `SimpleChar:79872F80` | L66, 25,318 HP, scale 107 | exact `052115` Nematet SCFU | `26159` | `(171.324936, 36.0112457, 340.074097)` |

The `052115` SCFUs observed alternate L54/10,415-HP Curator and
L68/26,500-HP Nematet generations. No level-selection formula is proven. The
runtime uses the newest fight generations while retaining the exact same-name,
same-monster-data appearance shapes from `052115`.

## The Curator

- Curator initiated combat before the player attacked. The player was about
  four horizontal units from the anchor, proving close proactive aggro but not
  an exact threshold. Runtime uses the existing conservative seven-unit Temple
  named policy.
- The captured normal stream is `33`, then `57`, `57`, all slot `0`, ammo `-1`,
  weapon instance `1465538645`.
- SpecialAttackWeapon contains `205877/205878`, tag `1465538645`, name `WZXU`,
  and envelope `381/381/381/31/0`.
- Nano `205565` starts `15.4643854` seconds after combat begins and repeats at
  an observed `10.1841983`-second interval. Two completed casts give the
  `6.2402402`-second median finish policy. No nano effect is assigned.
- Corpse `F54008` is linked to monster data `22802` and CATMesh `21499`.
- The exact first-open snapshot contains `377` credits plus `287143` QL200,
  `204758` QL1, and `204651` QL1, all quantity one.

## Nematet the Custodian of Time

- The player initiated the captured fight; proactive aggro is therefore not
  claimed.
- Captured normal streams are slot `2` for `82`, slot `0` for `70`, and slot
  `1` for `152`. Their exact weapon instances are `1497912661`, `1431525169`,
  and `1263026755` respectively.
- SpecialAttackWeapon contains `207327/207328 FUGB`, `207324/207325 YHUU`,
  `207321/207322 KHBC`, and `163491/163492 USW1`, with envelope
  `494/494/494/38/0`.
- The captured nano order is `205395`, four `205563` casts, `205395`, `205592`,
  then `205563`. The observed repeat median is `10.1701624` seconds. Cast-finish
  policies are `5.2211694`, `5.6058988`, and `3.6813144` seconds respectively.
  No nano effect is assigned.
- The boss chased roughly `28.6` horizontal units before death. No reset or
  return boundary was captured, so the shared 40-unit Temple named leash policy
  remains explicit policy rather than a parity claim.
- Corpse `F5401C` is linked to monster data `26159` and CATMesh `17909`.
- The exact first-open snapshot contains `2711` credits plus `287143` QL200,
  `204651` QL1, `204706` QL1, and `204595` QL1, all quantity one.

## Runtime boundary

Both actors are owned by `CapturedTempleOfThreeWindsEncounterRuntimeService`,
use PF1931-only `totw.*` profile/encounter keys, and register through the
Temple-specific combat and loot catalogs. Subway PF127 and ICC definitions are
unchanged.

## Deliberately unresolved

- Exact automatic-aggro threshold for Curator and any automatic aggro for Nematet.
- Nano stat/damage/debuff effects.
- Exact respawn time and complete loot-pool probabilities.
- PF1931 collision-aware pursuit and wall-aware line of sight.
- The official rule selecting the observed alternate level/health generations.
