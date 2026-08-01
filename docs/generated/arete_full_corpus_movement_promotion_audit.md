# Arete full-corpus movement promotion audit

## Result

The three complete Arete movement evidence sources reconcile deterministically to **26,654** independently classified paths: **23,185 promotable**, **1,853 ambiguous**, and **1,616 rejected**.

The aggregate runtime dataset contains **22,798** deduplicated patrol, spawn, chase, flee, and leash observations. Scripted runtime rows: **0**.

## Evidence searched

Complete corrected packet projections and their input hashes were read for:

- Capture `20260721-Rox-robots`:
  - `docs/generated/arete_20260721_rox_robots_movement/source/cleaning_robot_patrol_replay.csv` (`sha256 3f2b549145744da918a34f8f16a35d33039609529e1287513e8d89d3f38f76d7`)
  - `docs/generated/arete_20260722_104809_movement/patrol.csv` (`sha256 a117d53fa95a43331e500483f8e7ba984ef2dfe330321d0857b8ed044731f10b`)
  - `docs/generated/arete_20260722_152454_movement/patrol.csv` (`sha256 f6cb5d1121b5764ac7587a562acf2c41af208f87ed4d5e24b868c30fac3e9d0f`)
  - `docs/generated/arete_20260721_rox_robots_movement/manifest.json`
  - `docs/generated/arete_20260721_rox_robots_movement/runtime/manifest.json`
  - six behavior analysis CSVs and five non-scripted runtime CSVs under `docs/generated/arete_20260721_rox_robots_movement`
- Capture `20260722-104809`:
  - `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-104809/capture_info.json` (`sha256 9fb26887405925fe6fb84ccb28a2d672a5ae39a854ef48ed31bfa6fc535b4596`)
  - `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-104809/movement-summary.json` (`sha256 d53120d8e0f26514d626e299d1c34cd4baaa4cbf87e7fde9a846a935282b5b40`)
  - `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-104809/npc-lifecycle-summary.json` (`sha256 10efcbc1ba950481b706b721de088b677a2835a1bf7342b611c8dfbfb8aee030`)
  - `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-104809/movement-packets.csv` (`sha256 971a1db73804e50ce3bc66dd0125ce306d9c591daa490eca34f8b8a8e470defb`)
  - `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-104809/scfu-appearance.csv` (`sha256 995675740d4db81f491af3a32f5f3303265bdb5605271f0f5b0eeb16bf1da6c5`)
  - `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-104809/enemy-combat.csv` (`sha256 143b6690d4a74727c100a55a8c47f726118ba6f3c4ce89f7d3130823b8f23494`)
  - `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-104809/enemy-state.csv` (`sha256 355dc045181b0d44fd254ec070f63776fdc9d271d7323532af7a509c910d6d30`)
  - `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-104809/npc-lifecycle.csv` (`sha256 9f8d7b99b1be563b643adcf1892900de8590dfa1ed975ce114cc6fb637e2feba`)
  - `docs/generated/arete_20260722_104809_movement/manifest.json`
  - `docs/generated/arete_20260722_104809_movement/runtime/manifest.json`
  - six behavior analysis CSVs and five non-scripted runtime CSVs under `docs/generated/arete_20260722_104809_movement`
- Capture `20260722-152454`:
  - `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/capture_info.json` (`sha256 d1286dea8646ccf8eafc5f89196fd0d3884f8071a69506312e6822d5021aff98`)
  - `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/movement-summary.json` (`sha256 f535db286ca72df3ea35ce2f8d463eb99d1a740c75af5d8c6f9913a728723d17`)
  - `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/npc-lifecycle-summary.json` (`sha256 96eb334841a8284e2916240f3583f50eb4d095f0604a940bf1360eba5f6eca4b`)
  - `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/movement-packets.csv` (`sha256 93be20063e8397b6f91ddd2e24135f35289fbf3500736bda4103aabee21d5dc5`)
  - `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/scfu-appearance.csv` (`sha256 4292ddff4c0cbc26c7960c26a7dbf8bbb3c9f1dc0cdb0b9cf88389f0c337e5f9`)
  - `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/enemy-combat.csv` (`sha256 53c3e0f43b1b235121ae994ee973b96ff76968b2c531ff907a5d6395ccc1716f`)
  - `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/enemy-state.csv` (`sha256 f4bf9b1a73dd7e75f1515efa029b62d38f4e6a7a2e405452a35ba14fe3f93465`)
  - `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260722-152454/npc-lifecycle.csv` (`sha256 8c588d9d205b47da0dcbb550ccf72575fc8a251de92d7cc396a7ef83dbbaab87`)
  - `docs/generated/arete_20260722_152454_movement/manifest.json`
  - `docs/generated/arete_20260722_152454_movement/runtime/manifest.json`
  - six behavior analysis CSVs and five non-scripted runtime CSVs under `docs/generated/arete_20260722_152454_movement`

The corrected audit resolves identity metadata from the complete capture, including movement before SCFU; classifies and scores each observation before grouping; does not compare one packet's destination with the next packet's start as teleport evidence; and does not require a loop, repeated edge, or multiple runtime identities for patrol evidence.

## Deterministic reconciliation

| Capture | Reconciled | Promotable | Ambiguous | Rejected | Runtime rows | Route groups |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `20260721-Rox-robots` | 2,612 | 2,612 | 0 | 0 | 2,531 | 158 |
| `20260722-104809` | 14,516 | 12,344 | 909 | 1,263 | 12,146 | 2,884 |
| `20260722-152454` | 9,526 | 8,229 | 944 | 353 | 8,121 | 2,754 |

| Behavior | Observations | Promotable | Ambiguous | Rejected | Aggregate runtime rows |
| --- | ---: | ---: | ---: | ---: | ---: |
| patrol | 22,486 | 21,305 | 0 | 1,181 | 20,933 |
| spawn | 1,411 | 1,399 | 0 | 12 | 1,384 |
| chase | 1,053 | 164 | 496 | 393 | 164 |
| flee | 76 | 54 | 0 | 22 | 54 |
| leash | 271 | 263 | 0 | 8 | 263 |
| scripted | 1,357 | 0 | 1,357 | 0 | 0 |

## Promotable observations

Every promotable observation retains its captured family, template, level, captured/runtime playfield constraints, name, source identity, source generation, timestamp, sequence, route signature, coordinates, path count, and inter-observation delay. The only transformed fields are:

- `CaptureId`, added to make regenerated runtime identities capture-scoped.
- `ObservationId`, prefixed with `CaptureId:` to prevent cross-capture ID collisions.

Per-capture exact equivalents remain collapsed by the corrected audit. Distinct observations from different captures are retained because capture provenance, timestamps, identities, or ordering make them separate evidence; the aggregator does not invent or splice routes.

- patrol: **20,933** rows in `AORebirth/Server/ZoneEngine/Content/Captured/Arete/movement-full/patrol.csv`
- spawn: **1,384** rows in `AORebirth/Server/ZoneEngine/Content/Captured/Arete/movement-full/spawn.csv`
- chase: **164** rows in `AORebirth/Server/ZoneEngine/Content/Captured/Arete/movement-full/chase.csv`
- flee: **54** rows in `AORebirth/Server/ZoneEngine/Content/Captured/Arete/movement-full/flee.csv`
- leash: **263** rows in `AORebirth/Server/ZoneEngine/Content/Captured/Arete/movement-full/leash.csv`

Combat and player influence is preserved in the matching chase, flee, and leash behavior class. It is not used to contaminate independently clean patrol or spawn observations.

## Ambiguous observations — exact reasons

These observations remain in the per-capture analysis datasets with confidence and exact reasons, but are not promoted to runtime. Counts below are reason incidences; a single observation can carry more than one reason.

| Exact decision reason | Observation incidences |
| --- | ---: |
| `scripted_family_heuristic_only` | 1,357 |
| `post_combat_direction_not_leash` | 429 |
| `combat_target_position_unavailable` | 62 |
| `combat_direction_ambiguous` | 5 |

## Rejected observations — exact reasons

Rejected rows remain traceable in the per-capture behavior CSVs. They cannot contaminate promotable rows in the same route group.

| Exact decision reason | Observation incidences |
| --- | ---: |
| `metadata_unresolved` | 1,185 |
| `path_interrupted_by_stop_command` | 536 |
| `explicit_setpos_teleport` | 146 |
| `combat_target_position_unavailable` | 62 |
| `post_combat_direction_not_leash` | 26 |
| `combat_direction_ambiguous` | 2 |

## Remaining movement gaps

- Scripted movement trigger semantics remain unresolved for the observations classified as scripted; all scripted rows are deliberately excluded from runtime.
- Ambiguous observations remain unresolved only for the exact decision reasons above. The complete rows, confidence scores, influences, geometry, metadata resolution, and packet provenance remain available in the generated per-capture CSVs.
- Rejected observations remain unsupported for promotion for their exact recorded reasons; no clean observation is rejected merely because another observation sharing its route group is bad.

No available movement evidence was ignored because of a required loop, repeated edge, multiple identities, pre-existing runtime state, cross-packet destination comparison, or a rule that combat/player influence invalidates the appropriate combat movement class.

Aggregate manifest: `AORebirth/Server/ZoneEngine/Content/Captured/Arete/movement-full/manifest.json` (schema 4).
