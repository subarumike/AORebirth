# Corrected Arete Movement Promotion Audit — 20260722-152454

This is an observation-level, analysis-only audit. It does not modify AORebirth runtime behavior.

## Verdict

- Reconciled observations: **9,526 / 9,526**.
- Promotable: **8,229**.
- Ambiguous: **944**.
- Rejected: **353**.
- Post-decision route groups: **2,754**.

Every path is classified and scored before grouping. A rejected observation cannot alter a clean observation sharing the same route signature.

## Behavior datasets

| Behavior | Total | Promotable | Ambiguous | Rejected | Dataset |
| --- | ---: | ---: | ---: | ---: | --- |
| patrol | 7,393 | 7,305 | 0 | 88 | [`patrol.csv`](arete_20260722_152454_movement/patrol.csv) |
| spawn | 612 | 608 | 0 | 4 | [`spawn.csv`](arete_20260722_152454_movement/spawn.csv) |
| chase | 673 | 98 | 333 | 242 | [`chase.csv`](arete_20260722_152454_movement/chase.csv) |
| flee | 34 | 22 | 0 | 12 | [`flee.csv`](arete_20260722_152454_movement/flee.csv) |
| leash | 203 | 196 | 0 | 7 | [`leash.csv`](arete_20260722_152454_movement/leash.csv) |
| scripted | 611 | 0 | 611 | 0 | [`scripted.csv`](arete_20260722_152454_movement/scripted.csv) |

## Promotable observations — exact reasons

| Reason | Observations |
| --- | ---: |
| `complete_decoded_path` | 8,229 |
| `exact_identity_metadata` | 8,229 |
| `captured_patrol_observation` | 7,305 |
| `captured_spawn_observation` | 608 |
| `combat_influence_preserved_for_behavior` | 316 |
| `captured_leash_observation` | 196 |
| `captured_chase_observation` | 98 |
| `player_influence_preserved_for_behavior` | 35 |
| `captured_flee_observation` | 22 |

## Ambiguous observations — exact reasons

| Reason | Observations |
| --- | ---: |
| `scripted_family_heuristic_only` | 611 |
| `post_combat_direction_not_leash` | 302 |
| `combat_target_position_unavailable` | 27 |
| `combat_direction_ambiguous` | 4 |

## Rejected observations — exact reasons

| Reason | Observations |
| --- | ---: |
| `path_interrupted_by_stop_command` | 191 |
| `explicit_setpos_teleport` | 93 |
| `metadata_unresolved` | 90 |
| `combat_target_position_unavailable` | 43 |
| `post_combat_direction_not_leash` | 11 |
| `combat_direction_ambiguous` | 1 |

## Metadata resolution

| Resolution | Observations |
| --- | ---: |
| `later_scfu_same_generation` | 6,523 |
| `complete_capture_stable_identity` | 1,828 |
| `preceding_scfu_same_generation` | 1,085 |
| `unresolved` | 90 |

## Largest promotable route groups

| Behavior | Disposition | Family | Template | Level | PF | Signature | Observations | Confidence | Names |
| --- | --- | ---: | ---: | ---: | ---: | --- | ---: | --- | --- |
| patrol | Promotable | 1019 | 297023 | 1 | 1044525 | 2cb044179e1c0e0f | 1100 | 100–100 | Malfunctioning Cleaning Robot |
| patrol | Promotable | 1019 | 297023 | 1 | 1044525 | 7740d1949c568006 | 865 | 100–100 | Malfunctioning Cleaning Robot |
| patrol | Promotable | 1019 | 297023 | 1 | 1044525 | 8fdb70a3a252296e | 788 | 100–100 | Malfunctioning Cleaning Robot |
| patrol | Promotable | 1019 | 297023 | 1 | 1044525 | 2eb3373b9dba6082 | 660 | 100–100 | Malfunctioning Cleaning Robot |
| patrol | Promotable | 1019 | 297023 | 1 | 1044525 | 52aa3d9899596478 | 179 | 100–100 | Malfunctioning Cleaning Robot |
| spawn | Promotable | 1019 | 297023 | 1 | 1044525 | 2cb044179e1c0e0f | 83 | 100–100 | Malfunctioning Cleaning Robot |
| spawn | Promotable | 1019 | 297023 | 1 | 1044525 | 7740d1949c568006 | 63 | 100–100 | Malfunctioning Cleaning Robot |
| patrol | Promotable | 1019 | 297023 | 1 | 1044525 | e432157bed51e514 | 54 | 100–100 | Malfunctioning Cleaning Robot |
| patrol | Promotable | 1019 | 297023 | 1 | 1044525 | 6362d488a88ec42b | 53 | 100–100 | Malfunctioning Cleaning Robot |
| spawn | Promotable | 1019 | 297023 | 1 | 1044525 | 2eb3373b9dba6082 | 50 | 100–100 | Malfunctioning Cleaning Robot |
| patrol | Promotable | 1019 | 297023 | 1 | 1044525 | 902fa87c25bf8293 | 50 | 100–100 | Malfunctioning Cleaning Robot |
| patrol | Promotable | 1019 | 297023 | 1 | 1044525 | 21fea6b658f3944b | 43 | 100–100 | Malfunctioning Cleaning Robot |
| spawn | Promotable | 1019 | 297023 | 1 | 1044525 | 8fdb70a3a252296e | 42 | 100–100 | Malfunctioning Cleaning Robot |
| patrol | Promotable | 1019 | 297023 | 1 | 1044525 | a786c0bf2ba3770b | 35 | 100–100 | Malfunctioning Cleaning Robot |
| patrol | Promotable | 25 | 17657 | 2 | 1044525 | 1857a3c149770fed | 32 | 100–100 | Garbage Flea |

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
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/capture_info.json` | 9,350 | `d1286dea8646ccf8eafc5f89196fd0d3884f8071a69506312e6822d5021aff98` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/movement-summary.json` | 482 | `f535db286ca72df3ea35ce2f8d463eb99d1a740c75af5d8c6f9913a728723d17` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/npc-lifecycle-summary.json` | 6,887 | `96eb334841a8284e2916240f3583f50eb4d095f0604a940bf1360eba5f6eca4b` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/movement-packets.csv` | 4,494,449 | `93be20063e8397b6f91ddd2e24135f35289fbf3500736bda4103aabee21d5dc5` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/scfu-appearance.csv` | 4,636,245 | `4292ddff4c0cbc26c7960c26a7dbf8bbb3c9f1dc0cdb0b9cf88389f0c337e5f9` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/enemy-combat.csv` | 3,499,753 | `53c3e0f43b1b235121ae994ee973b96ff76968b2c531ff907a5d6395ccc1716f` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/enemy-state.csv` | 24,222,879 | `f4bf9b1a73dd7e75f1515efa029b62d38f4e6a7a2e405452a35ba14fe3f93465` |
| `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/npc-lifecycle.csv` | 1,825,269 | `8c588d9d205b47da0dcbb550ccf72575fc8a251de92d7cc396a7ef83dbbaab87` |

## Validation

- Capture lifecycle complete: `true`
- Capture processing allowed: `true`
- SCFU pending/errors: `0/0`
- Movement decode errors: `0`
- Dataset manifest: [`manifest.json`](arete_20260722_152454_movement/manifest.json)
- Report schema: `2`
