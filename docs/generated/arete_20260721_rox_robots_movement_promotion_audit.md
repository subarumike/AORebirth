# Arete 20260721 robot movement promotion audit

## Result

The capture-scoped legacy projection reconciles to **2,612** promotable patrol observations and **2,531** deduplicated schema-3 runtime rows.

## Evidence used

- `docs/generated/arete_20260721_rox_robots_movement/source/cleaning_robot_patrol_replay.csv`
- `docs/generated/arete_20260722_104809_movement/patrol.csv` (exact promoted route and NPC metadata correlation)
- `docs/generated/arete_20260722_152454_movement/patrol.csv` (exact promoted route and NPC metadata correlation)
- Capture: `20260721-Rox-robots`
- Exact identities: 10
- Source sha256: `3f2b549145744da918a34f8f16a35d33039609529e1287513e8d89d3f38f76d7`

## Exact behavior promoted

Every complete inbound `FollowTarget/NpcPath` observation for the observed Malfunctioning Cleaning Robot identities is promoted as patrol with its original timestamp, sequence, coordinates, identity, family, template, level, captured playfield, and generation 0.

The final row for each identity has an exact terminal delay of zero (10 terminal rows). Schema-4 completion therefore falls back normally and cannot wrap into the removed legacy replay loop.

## Evidence gaps preserved

- This projection proves patrol only; it does not infer spawn, chase, flee, leash, or scripted behavior.
- No legacy patrol packet exists for spawn-cohort identity `SimpleChar:7986653C`; no route is synthesized for it.
- It does not invent route closures, return edges, waypoint fallbacks, or repeat timing.

The source projection remains under generated non-runtime provenance. No available legacy robot movement evidence was discarded.

## Deterministic reproduction

- `python tools-temp/AOSharpCaptureAnalyzer/promote_arete_legacy_robot_movement.py --write`
- `python tools-temp/AOSharpCaptureAnalyzer/aggregate_arete_movement_runtime.py --write`
