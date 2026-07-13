# Subway 2026-07-09 Capture Population Batch

## Evidence

- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260709-205921`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260709-210452`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260709-212115`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260709-212336`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260709-220439`
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260709-222339`

The crash-truncated first capture remains usable through its flushed packet, movement, combat, and survey logs. The later population captures finalized successfully. The vendor-only capture did not finalize its metadata, but its five completed shop update sequences remain usable evidence.

## Recovered packet evidence

`tools-temp/AOSharpCaptureAnalyzer` decodes `SimpleCharFullUpdate` packets directly from each capture's lossless `packets.hex.log`. It recovered 2,441 SCFU rows with zero decode failures:

- `20260709-205921`: 59
- `20260709-210452`: 206
- `20260709-212115`: 365
- `20260709-212336`: 310
- `20260709-220439`: 788
- `20260709-222339`: 713

The generated `scfu-appearance.csv` files preserve identity, position, heading, level, health, run speed, NPC family, flags, owner, appearance value, monster data, scale, head mesh, textures, meshes, waypoints, and texture overrides. This allows future decoding work without repeating the live traversal.

## Implemented population

The prior 32-entry supported population subset was replaced rather than appended, preventing duplicate populations. The current baseline contains 95 identities: 77 from the completed `20260709-212336` survey, eight deeper Filth Flea positions from `20260709-220439`, and nine Filth Flea plus one Mugger position from the completion capture `20260709-222339`. Later same-position SCFU identities were classified as respawns and were not added as duplicate server spawns.

| Archetype | Count |
| --- | ---: |
| Filth Flea | 51 |
| Discarded Pet | 18 |
| Violent Vagabond | 11 |
| Disobedient Bot | 10 |
| Mugger | 4 |
| Thief | 1 |

Each definition uses captured health, level, position, monster scale, and run speed. The four previously proven periodic patrol loops remain assigned to the corresponding current captured identities. Packet-backed textures and meshes are applied to Mugger and Violent Vagabond; the existing Thief visual path remains preserved.

## Ordinary archetype slice

The ordinary-archetype provider adds 126 spatially deduplicated spawns without database mob templates. Workman and Architect Striker share the `striker` family but preserve separate captured `monsterData` and visual profiles.

| Archetype | Spawns | monsterData | Spawn/SCFU capture evidence |
| --- | ---: | ---: | --- |
| Shadow | 31 | 30464 | `20260709-212336`, `20260709-222339` |
| Stim Fiend | 9 | 203739 | `20260709-212336`, `20260709-222339` |
| Workman Striker | 21 | 203854 | `20260709-212336`, `20260709-222339` |
| Architect Striker | 7 | 203743 | `20260709-212336` |
| Infected Attendant | 5 | 96056 | `20260709-212336`, `20260709-222339` |
| Slum Runner | 24 | 55648 | `20260709-212336`, `20260709-222339` |
| Looter | 6 | 203745 | `20260709-212336` |
| Infector | 12 | 31909 | `20260709-222339` |
| Lost Thought | 4 | 96193 | `20260709-222339` |
| Neural Burnout | 7 | 203730 | `20260709-222339` |

The generator combines those spawn SCFUs with matching enemy dossier, movement/path, `enemy-combat.csv`, corpse events, and `inventory-updates.csv` rows from all six completed capture directories. It preserves captured level, health, scale, run speed, family/LOS, flags, SCFU unknown bytes, textures, meshes/material overrides, headings, waypoints, observed attack damage/timing/AttackInfo fields, and observed item template/quality evidence. Same-name positions within 1.5 world units are represented once; owned Infector identities `795451A1` and `795451A9` are excluded because their captured owner is Abmouth Supremus.

The runtime constructs ordinary `Character` instances directly rather than calling `SpawnMobFromTemplate`. A per-instance evidence registry then supplies exact optional SCFU fields, captured combat profiles, path data, and captured corpse-loot entries while retaining the existing corpse access, transfer, credits, and despawn lifecycle.

### Explicit evidence gaps

- Lost Thought has no observed outgoing `AttackInfo`; its combat evidence remains unobserved and no damage value is fabricated.
- Infected Attendant and Neural Burnout have observed damage/AttackInfo rows but no repeat interval; the existing safe combat-tick default is used instead of inferring a recharge value.
- Lost Thought and Neural Burnout have no matched opened-corpse inventory evidence; their captured loot lists are empty.
- Spawns without captured SCFU waypoints remain static; no path is copied from another NPC.
- No ordinary-specific corpse cat-mesh override was captured, so the existing generic corpse lifecycle is retained without a guessed visual template.

## Deferred named/boss evidence

The captures also contain Strike Foreman, Abmouth Supremus, Eumenides, Vergil Aeneid, Bitaxel, Bloodcreeper, Empty Shell, Fragmented Soul, Incomplete Rebuild, Melded Patterns, Molested Molecules, Premature Pattern, Redundant Scan, and other named/deep content. Those entities remain outside this slice and were not assigned guessed templates or ordinary behavior. `Healer` is Mike's personal pet, not a Subway enemy, and is excluded from population planning.

## Follow-up population restoration

Completed capture `20260710-202132` supplies a separate 38-row restoration: 29 supported-family rows and nine ordinary rows consisting of two Looters, six Stim Fiends, and one Deranged Shopper. The complete 107-identity disposition is recorded in `subway_20260710_population_restore_manifest.csv`, with the readable reconstruction report in `subway_20260710_population_restore.md`.

The follow-up uses exact captured source identities and coordinates without relocation. Historical commit `c2ebdb07` was inspected as evidence for the prior intent, but no historical commit was cherry-picked and the overbroad rollback `e9405ab8` was not reverted. Named bosses, owned summons, unsupported families, and duplicates remain excluded. The later RoomSpace investigation established that the previous instability was client-side; this population restoration adds no RoomSpace workaround or coordinate suppression.

## Validation

- `AOSharpCaptureAnalyzer` Debug/x86 build: PASS.
- Six-capture SCFU decode: PASS, 2,441 rows, zero failures.
- Approved AORebirth Debug build: PASS after stopping the running ZoneEngine that held the output file.
- Chat/Login/Zone restart: PASS; ports `6996`, `7012`, `7500`, and `7501` listening.
- Live AORebirth gameplay smoke: not performed.
