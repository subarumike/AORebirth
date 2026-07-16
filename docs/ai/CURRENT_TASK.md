# Current Task

## Current Focus

Validate the new PF127 geometry-aware hostile-NPC chase path in the private client: Vergil must route around a blocking Subway wall, regain a clear shot, and resume his unchanged captured combat behavior.

## Done in this slice

- Promoted PF127 collision geometry from completed geometry-only safe capture `20260714-185728` into the server content asset.
- Added fail-closed geometry loading, segment/triangle collision queries, and contract-gated NPC damage line-of-sight checks.
- Enabled the LOS requirement for Vergil Aeneid without changing unrelated NPC combat contracts.
- Mike live-validated that Vergil can no longer damage the player through walls and resumes attacking with clear LOS.
- Added geometry-only capture safety, snapshot/promotion validation, analyzer support, and focused regression coverage.
- Synced the work with the current remote Mail subsystem and preserved both sets of `ZoneEngine.csproj` entries.
- Added a global `ZoneEngine.Core.Navigation` chase service with explicit playfield capability providers, bounded deterministic route planning, reusable route state, collision-valid route following, retry suppression, and complete NPC lifecycle cleanup.
- Enabled PF127 as the first provider using the promoted authoritative collision geometry; other playfields remain explicitly unsupported and retain legacy direct chase.
- Routed shared hostile-NPC combat movement through the navigation boundary without changing enemy damage, weapon context, cadence, aggro, patrol, pet, or return-to-spawn behavior.
- Added deterministic shared-architecture and PF127 representative Vergil route/follower coverage. PF127 is same-elevation only until authoritative floor/connectivity data exists.
- AORebirth.Core and ZoneEngine Debug builds pass. Navigation `29/29`, PF127 collision/LOS `17/17`, Abmouth/Vergil `19/19`, geometry-only safe mode `9/9`, and capture runtime safety `6/6` pass; lifecycle remains at the same six unrelated baseline guardrail failures.

## Remaining

1. Mike performs one private-client Vergil obstruction playtest: engage normally, move behind the representative wall, and observe whether Vergil routes around it and resumes firing only after the wall no longer blocks him.
2. If client-visible movement differs from the deterministic route tests, inspect the existing server logs/capture evidence before changing planner limits or movement packets.
3. Do not auto-attach or launch AO/capture tooling. Mike runs gameplay and supplies completed captures when requested.

## Constraints

- The chase architecture is global, but PF127/resource `127` is the only enabled provider for this gameplay slice.
- The promoted collision asset and LOS gate fail closed when evidence or geometry is missing/invalid.
- Existing working Subway combat, loot, corpse, respawn, and population behavior must remain unchanged.
