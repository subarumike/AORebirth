# Robot Patrol Replay Data Promotion

Generated: 2026-07-01 local

> Historical replay-path report. Superseded by the schema-4 full-corpus movement runtime and the deterministic `20260721-Rox-robots` normalization in `docs/generated/arete_20260721_rox_robots_movement_promotion_audit.md`. The looping runtime path documented below was removed; its source rows remain preserved as generated provenance.

## Scope

This report documents the promotion of captured `Malfunctioning Cleaning Robot` idle patrol replay data out of local `tools-temp` capture output and into committed runtime/evidence data.

No movement algorithm, replay cadence, combat behavior, corpse/despawn behavior, packet type, or waypoint behavior was intentionally changed.

## Source Evidence

- Source capture folder: `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260629-193121`
- Source CSV: `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260629-193121/movement-packets.csv`
- Promoted rows: `FollowTarget` rows with `FollowKind=NpcPath` for the seven captured Arete `Malfunctioning Cleaning Robot` source identities.

## Runtime Data

- Historical runtime/evidence CSV: `AORebirth/Server/ZoneEngine/Content/Captured/Arete/cleaning_robot_patrol_replay.csv` (removed after consolidation)
- Current provenance CSV: `docs/generated/arete_20260721_rox_robots_movement/source/cleaning_robot_patrol_replay.csv`
- Current runtime owner: `CapturedAreteMovementRuntimeService` through `movement-full` schema 4
- Runtime coordinator: `NpcPatrolReplayCoordinator`
- Runtime executor: `NPCController`

Raw capture output remains evidence only. Runtime patrol replay now loads committed data.

## Promoted Row Counts

| SourceInstance | Rows |
| --- | ---: |
| `79225E7D` | 35 |
| `79225E7C` | 40 |
| `79225E77` | 38 |
| `79225E7A` | 31 |
| `79225E78` | 39 |
| `79225E79` | 29 |
| `79225E76` | 18 |

Total promoted rows: `230`.
