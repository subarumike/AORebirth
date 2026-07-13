# Subway 20260710 Population Restore

Authoritative capture: `20260710-202132` (complete; processing allowed).

The historical population commit `c2ebdb07` is evidence only. The overbroad safety rollback `e9405ab8` is not reverted wholesale. The later RoomSpace investigation proved the client crash was not caused by these captured coordinates, and this restoration adds no RoomSpace workaround.

## Classification summary

| Classification | Rows |
| --- | ---: |
| DUPLICATE_EXCLUDED | 18 |
| MALFORMED_OR_INCOMPLETE | 0 |
| NAMED_BOSS_EXCLUDED | 0 |
| ORDINARY_ENEMY_REGENERATE | 9 |
| OWNED_SUMMON_EXCLUDED | 2 |
| SUPPORTED_FAMILY_RESTORE | 29 |
| UNSUPPORTED_FAMILY_EXCLUDED | 49 |

## Included rows

| Identity | Enemy | Position | Level | Classification | Implementation path |
| --- | --- | --- | ---: | --- | --- |
| `(SimpleChar:79557C09)` | Discarded Pet | `(183.01, 107.611687, 308.6345)` | 9 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Discarded Pet |
| `(SimpleChar:79557C26)` | Discarded Pet | `(192.565231, 107.611687, 289.6804)` | 7 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Discarded Pet |
| `(SimpleChar:79557C31)` | Discarded Pet | `(174.194214, 107.61483, 242.166443)` | 5 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Discarded Pet |
| `(SimpleChar:79557C66)` | Disobedient Bot | `(151.409119, 107.61483, 271.044)` | 7 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Disobedient Bot |
| `(SimpleChar:79557C8B)` | Discarded Pet | `(286.2218, 107.611687, 285.7219)` | 10 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Discarded Pet |
| `(SimpleChar:79557CA7)` | Discarded Pet | `(161.97876, 107.613258, 301.466125)` | 8 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Discarded Pet |
| `(SimpleChar:79557CAB)` | Discarded Pet | `(281.3582, 107.611687, 284.467255)` | 10 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Discarded Pet |
| `(SimpleChar:79557CAC)` | Violent Vagabond | `(273.7663, 107.611687, 284.703522)` | 10 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Violent Vagabond |
| `(SimpleChar:79557CAD)` | Discarded Pet | `(288.673035, 107.611687, 276.390656)` | 10 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Discarded Pet |
| `(SimpleChar:79557CB8)` | Looter | `(284.850769, 107.611687, 294.085632)` | 10 | ORDINARY_ENEMY_REGENERATE | CapturedSubwayOrdinaryContentProvider archetype: Looter |
| `(SimpleChar:79557F12)` | Stim Fiend | `(287.733978, 107.611687, 299.437225)` | 11 | ORDINARY_ENEMY_REGENERATE | CapturedSubwayOrdinaryContentProvider archetype: Stim Fiend |
| `(SimpleChar:79557F14)` | Mugger | `(292.5373, 107.611687, 298.02475)` | 10 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Mugger |
| `(SimpleChar:7957405C)` | Violent Vagabond | `(166.46637, 107.6164, 165.103058)` | 7 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Violent Vagabond |
| `(SimpleChar:795743A7)` | Violent Vagabond | `(197.541138, 108.416405, 209.092392)` | 10 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Violent Vagabond |
| `(SimpleChar:795743A8)` | Violent Vagabond | `(199.9471, 108.416405, 193.514114)` | 10 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Violent Vagabond |
| `(SimpleChar:79574527)` | Deranged Shopper | `(255.7054, 107.611687, 285.020325)` | 8 | ORDINARY_ENEMY_REGENERATE | CapturedSubwayOrdinaryContentProvider archetype: Deranged Shopper |
| `(SimpleChar:7957E02C)` | Violent Vagabond | `(169.272583, 107.61483, 244.71405)` | 7 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Violent Vagabond |
| `(SimpleChar:7957E02E)` | Violent Vagabond | `(163.902328, 107.6164, 164.683487)` | 7 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Violent Vagabond |
| `(SimpleChar:7957E123)` | Violent Vagabond | `(149.739487, 107.61483, 279.861847)` | 6 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Violent Vagabond |
| `(SimpleChar:7957E128)` | Stim Fiend | `(287.055054, 107.611687, 310.951843)` | 12 | ORDINARY_ENEMY_REGENERATE | CapturedSubwayOrdinaryContentProvider archetype: Stim Fiend |
| `(SimpleChar:7957E40A)` | Disobedient Bot | `(211.504623, 107.6164, 166.472961)` | 10 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Disobedient Bot |
| `(SimpleChar:7957E40E)` | Violent Vagabond | `(182.846771, 107.6164, 165.3118)` | 6 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Violent Vagabond |
| `(SimpleChar:7957E411)` | Discarded Pet | `(201.890152, 107.6164, 164.699)` | 10 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Discarded Pet |
| `(SimpleChar:7957E415)` | Stim Fiend | `(197.723587, 107.6164, 168.280075)` | 9 | ORDINARY_ENEMY_REGENERATE | CapturedSubwayOrdinaryContentProvider archetype: Stim Fiend |
| `(SimpleChar:7957E4A5)` | Discarded Pet | `(144.8586, 107.61483, 251.138519)` | 6 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Discarded Pet |
| `(SimpleChar:7957E4B1)` | Discarded Pet | `(151.498718, 107.61483, 237.92157)` | 5 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Discarded Pet |
| `(SimpleChar:7957E4BC)` | Discarded Pet | `(156.301163, 107.61483, 233.5397)` | 8 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Discarded Pet |
| `(SimpleChar:7957E5BF)` | Violent Vagabond | `(165.985245, 107.613258, 305.1552)` | 7 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Violent Vagabond |
| `(SimpleChar:7957E5C4)` | Violent Vagabond | `(153.280945, 107.61483, 277.751068)` | 7 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Violent Vagabond |
| `(SimpleChar:7957E5C5)` | Violent Vagabond | `(151.613754, 107.61483, 280.145721)` | 6 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Violent Vagabond |
| `(SimpleChar:7957E5C6)` | Mugger | `(152.437408, 107.613258, 297.01)` | 9 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Mugger |
| `(SimpleChar:7957E5C7)` | Mugger | `(153.4413, 107.613258, 297.974335)` | 8 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Mugger |
| `(SimpleChar:7957E5C8)` | Mugger | `(145.386154, 107.613258, 289.6806)` | 8 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Mugger |
| `(SimpleChar:7957E5CA)` | Mugger | `(267.640045, 107.611687, 287.824371)` | 10 | SUPPORTED_FAMILY_RESTORE | CapturedSubwayContentProvider shared family: Mugger |
| `(SimpleChar:7957E5CD)` | Looter | `(230.674591, 107.611687, 290.99)` | 9 | ORDINARY_ENEMY_REGENERATE | CapturedSubwayOrdinaryContentProvider archetype: Looter |
| `(SimpleChar:7957E5CF)` | Stim Fiend | `(277.575073, 107.611687, 275.633026)` | 10 | ORDINARY_ENEMY_REGENERATE | CapturedSubwayOrdinaryContentProvider archetype: Stim Fiend |
| `(SimpleChar:7957E5D0)` | Stim Fiend | `(290.75827, 107.611687, 283.753021)` | 10 | ORDINARY_ENEMY_REGENERATE | CapturedSubwayOrdinaryContentProvider archetype: Stim Fiend |
| `(SimpleChar:7957E5D1)` | Stim Fiend | `(292.3236, 107.611687, 294.727081)` | 10 | ORDINARY_ENEMY_REGENERATE | CapturedSubwayOrdinaryContentProvider archetype: Stim Fiend |

## Evidence boundaries

- Exact identity, position, heading, level, health, scale, run speed, family, flags, appearance, owner, and waypoints come from `scfu-appearance.csv` decoded from raw SCFU packets.
- Movement is applied only when the identity has captured movement/waypoint evidence.
- Looter and Stim Fiend reuse the existing capture-generated ordinary archetypes; Deranged Shopper receives its capture-generated archetype with observed 9-point AttackInfo and no inferred loot.
- Named bosses, player/encounter-owned summons, unsupported families, and cross-capture duplicates remain excluded.
- No coordinate mutation, RoomSpace workaround, boss mechanic, global combat timing change, or unrelated loot change is part of this restoration.

## Validation

- Manifest regeneration: PASS, 107 unique identities classified, 29 supported-family restores, 9 ordinary regenerations, 38 total included, zero malformed rows.
- Focused supported population, ordinary population, patrol, identity, position, exclusion, and lifecycle guardrails: PASS.
- `PlayfieldLifecycleTraceTests`: 41/46 PASS. The five remaining failures are the pre-existing announcement/session/visibility architecture guardrail mismatches and are outside this population slice.
- `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS.
- Chat/Login/Zone restart: PASS; ports `6996`, `7012`, `7500`, and `7501` listening.
- Live client traversal: pending Mike; no AO client was launched for this task.
