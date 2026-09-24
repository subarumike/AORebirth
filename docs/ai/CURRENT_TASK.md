# Current Task

Collision and movement now run on the vendored Lost-Eden Vehicle port
(`AORebirth.World.Vehicle`): `CharVehicleSim` for players, `NpcVehicleSim` + `Path_t`/guide
for NPCs, against a `TilemapSurface` + `CellSurface` built by `PlayfieldSurfaceFactory`.
Bepu is removed. Still open:

- Live validation (engines + zoneengine MCP) on a shell that has `AO_REBIRTH_MYSQL_CONNECTION`.
- `GraphPathFinder_t` instead of Recast for NPC route planning.
