# Corrected Arete Movement Promotion Audit — 20260722-104809

This is an observation-level, analysis-only audit. It does not modify AORebirth runtime behavior.

## Verdict

- Reconciled observations: **14,516 / 14,516**.
- Promotable: **12,344**.
- Ambiguous: **909**.
- Rejected: **1,263**.
- Post-decision route groups: **2,884**.

Every path is classified and scored before grouping. A rejected observation cannot alter a clean observation sharing the same route signature.

## Behavior datasets

| Behavior | Total | Promotable | Ambiguous | Rejected | Dataset |
| --- | ---: | ---: | ---: | ---: | --- |
| patrol | 12,481 | 11,388 | 0 | 1,093 | [`patrol.csv`](arete_20260722_104809_movement/patrol.csv) |
| spawn | 799 | 791 | 0 | 8 | [`spawn.csv`](arete_20260722_104809_movement/spawn.csv) |
| chase | 380 | 66 | 163 | 151 | [`chase.csv`](arete_20260722_104809_movement/chase.csv) |
| flee | 42 | 32 | 0 | 10 | [`flee.csv`](arete_20260722_104809_movement/flee.csv) |
| leash | 68 | 67 | 0 | 1 | [`leash.csv`](arete_20260722_104809_movement/leash.csv) |
| scripted | 746 | 0 | 746 | 0 | [`scripted.csv`](arete_20260722_104809_movement/scripted.csv) |

## Promotable observations — exact reasons

| Reason | Observations |
| --- | ---: |
| `complete_decoded_path` | 12,344 |
| `exact_identity_metadata` | 12,344 |
| `captured_patrol_observation` | 11,388 |
| `captured_spawn_observation` | 791 |
| `combat_influence_preserved_for_behavior` | 165 |
| `captured_leash_observation` | 67 |
| `captured_chase_observation` | 66 |
| `player_influence_preserved_for_behavior` | 33 |
| `captured_flee_observation` | 32 |

## Ambiguous observations — exact reasons

| Reason | Observations |
| --- | ---: |
| `scripted_family_heuristic_only` | 746 |
| `post_combat_direction_not_leash` | 127 |
| `combat_target_position_unavailable` | 35 |
| `combat_direction_ambiguous` | 1 |

## Rejected observations — exact reasons

| Reason | Observations |
| --- | ---: |
| `metadata_unresolved` | 1,095 |
| `path_interrupted_by_stop_command` | 345 |
| `explicit_setpos_teleport` | 53 |
| `combat_target_position_unavailable` | 19 |
| `post_combat_direction_not_leash` | 15 |
| `combat_direction_ambiguous` | 1 |

## Metadata resolution

| Resolution | Observations |
| --- | ---: |
| `later_scfu_same_generation` | 9,565 |
| `complete_capture_stable_identity` | 3,591 |
| `unresolved` | 1,095 |
| `preceding_scfu_same_generation` | 265 |

## Runtime promotion datasets

- Promotable source observations represented: **12,344**.
- Deduplicated runtime rows: **12,146**.
- Scripted observations included in runtime: **0**.

| Behavior | Source observations | Runtime rows | Runtime dataset |
| --- | ---: | ---: | --- |
| patrol | 11,388 | 11,194 | `docs/generated/arete_20260722_104809_movement/runtime/patrol.csv` |
| spawn | 791 | 787 | `docs/generated/arete_20260722_104809_movement/runtime/spawn.csv` |
| chase | 66 | 66 | `docs/generated/arete_20260722_104809_movement/runtime/chase.csv` |
| flee | 32 | 32 | `docs/generated/arete_20260722_104809_movement/runtime/flee.csv` |
| leash | 67 | 67 | `docs/generated/arete_20260722_104809_movement/runtime/leash.csv` |

## Largest promotable route groups

| Behavior | Disposition | Family | Template | Level | PF | Signature | Observations | Confidence | Names |
| --- | --- | ---: | ---: | ---: | ---: | --- | ---: | --- | --- |
| patrol | Promotable | 1019 | 297023 | 1 | 1044525 | 2cb044179e1c0e0f | 1671 | 100–100 | Malfunctioning Cleaning Robot |
| patrol | Promotable | 1019 | 297023 | 1 | 1044525 | 7740d1949c568006 | 1281 | 100–100 | Malfunctioning Cleaning Robot |
| patrol | Promotable | 1019 | 297023 | 1 | 1044525 | 8fdb70a3a252296e | 1170 | 100–100 | Malfunctioning Cleaning Robot |
| patrol | Promotable | 1019 | 297023 | 1 | 1044525 | 2eb3373b9dba6082 | 956 | 100–100 | Malfunctioning Cleaning Robot |
| patrol | Promotable | 1019 | 297023 | 1 | 1044525 | 52aa3d9899596478 | 255 | 100–100 | Malfunctioning Cleaning Robot |
| patrol | Promotable | 25 | 17657 | 1 | 1044525 | d0b7a926a7f91dea | 103 | 100–100 | Garbage Flea |
| patrol | Promotable | 25 | 17657 | 1 | 1044525 | 1857a3c149770fed | 99 | 100–100 | Garbage Flea |
| patrol | Promotable | 25 | 17657 | 1 | 1044525 | 817b4615fa0753a1 | 94 | 100–100 | Garbage Flea |
| patrol | Promotable | 25 | 17657 | 1 | 1044525 | f532a072080c8762 | 84 | 100–100 | Garbage Flea |
| patrol | Promotable | 1019 | 297023 | 1 | 1044525 | 902fa87c25bf8293 | 79 | 100–100 | Malfunctioning Cleaning Robot |
| patrol | Promotable | 25 | 17657 | 1 | 1044525 | b7808588353c4b38 | 66 | 100–100 | Garbage Flea |
| patrol | Promotable | 25 | 17657 | 1 | 1044525 | 5ea39d37b39df754 | 64 | 100–100 | Garbage Flea |
| spawn | Promotable | 1019 | 297023 | 1 | 1044525 | 2cb044179e1c0e0f | 63 | 100–100 | Malfunctioning Cleaning Robot |
| patrol | Promotable | 1019 | 297023 | 1 | 1044525 | ce6556a501b86310 | 49 | 100–100 | Malfunctioning Cleaning Robot |
| patrol | Promotable | 25 | 17657 | 6 | 1044525 | 03fa3d0f318ec29c | 42 | 100–100 | Garbage Flea |

## Corrected method

- Each decoded `FollowTarget/NpcPath` packet is one observation.
- Complete-capture SCFU metadata may resolve movement preceding SCFU when the same generation or stable reused identity has one exact metadata tuple.
- Runtime identity and lifecycle generation are retained only as evidence columns; the reusable group key is behavior, disposition, NPC family, template, level, playfield, and route signature.
- Route grouping happens only after observation disposition. Reasons are aggregated for reporting and never propagated between observations.
- Teleport rejection requires an explicit nearby `SetPos`; no previous-destination versus next-current comparison is performed.
- StopMovingCmd is the only movement-packet interruption rejection.
- Patrol evidence does not require a closed loop, repeated edge, or multiple identities.
- Combat and player influence are retained for chase, flee, and leash observations.
- Scripted classification remains ambiguous when supported only by the bounded family heuristic.

## Deterministic inputs

| Path | Bytes | SHA-256 |
| --- | ---: | --- |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-104809/capture_info.json` | 9,176 | `9fb26887405925fe6fb84ccb28a2d672a5ae39a854ef48ed31bfa6fc535b4596` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-104809/movement-summary.json` | 483 | `d53120d8e0f26514d626e299d1c34cd4baaa4cbf87e7fde9a846a935282b5b40` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-104809/npc-lifecycle-summary.json` | 6,994 | `10efcbc1ba950481b706b721de088b677a2835a1bf7342b611c8dfbfb8aee030` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-104809/movement-packets.csv` | 9,476,388 | `971a1db73804e50ce3bc66dd0125ce306d9c591daa490eca34f8b8a8e470defb` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-104809/scfu-appearance.csv` | 5,094,953 | `995675740d4db81f491af3a32f5f3303265bdb5605271f0f5b0eeb16bf1da6c5` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-104809/enemy-combat.csv` | 4,094,954 | `143b6690d4a74727c100a55a8c47f726118ba6f3c4ce89f7d3130823b8f23494` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-104809/enemy-state.csv` | 36,831,310 | `355dc045181b0d44fd254ec070f63776fdc9d271d7323532af7a509c910d6d30` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-104809/npc-lifecycle.csv` | 1,968,185 | `9f8d7b99b1be563b643adcf1892900de8590dfa1ed975ce114cc6fb637e2feba` |

## Validation

- Capture lifecycle complete: `true`
- Capture processing allowed: `true`
- SCFU pending/errors: `0/0`
- Movement decode errors: `0`
- Dataset manifest: [`manifest.json`](arete_20260722_104809_movement/manifest.json)
- Report schema: `3`
