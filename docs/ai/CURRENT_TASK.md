# Current Task

## Current Focus

Complete PF127 ranged-enemy behavior after the damage line-of-sight repair: Vergil must route around blocking Subway walls to regain a clear shot instead of remaining stationary behind them.

## Done in this slice

- Promoted PF127 collision geometry from completed geometry-only safe capture `20260714-185728` into the server content asset.
- Added fail-closed geometry loading, segment/triangle collision queries, and contract-gated NPC damage line-of-sight checks.
- Enabled the LOS requirement for Vergil Aeneid without changing unrelated NPC combat contracts.
- Mike live-validated that Vergil can no longer damage the player through walls and resumes attacking with clear LOS.
- Added geometry-only capture safety, snapshot/promotion validation, analyzer support, and focused regression coverage.
- Synced the work with the current remote Mail subsystem and preserved both sets of `ZoneEngine.csproj` entries.

## Remaining

1. Implement and validate PF127 chase/path selection around blocking walls without weakening the proven damage LOS gate.
2. Keep the movement change capture-backed; do not invent a general navigation system from the LOS result alone.
3. Do not auto-attach or launch AO/capture tooling. Mike runs gameplay and supplies completed captures when requested.

## Constraints

- PF127/resource `127` only for the active gameplay slice.
- The promoted collision asset and LOS gate fail closed when evidence or geometry is missing/invalid.
- Existing working Subway combat, loot, corpse, respawn, and population behavior must remain unchanged.
