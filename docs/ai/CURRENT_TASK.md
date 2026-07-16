# Current Task

## Current Focus

Validate the corrected PF127 doorway attack line and combat leash in the private client: Vergil must fire through the visibly open doorway, remain blocked by real walls, and disengage/return home instead of chasing across the full Subway playfield.

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
- Reproduced the reported open-doorway failure against the promoted geometry: the raw ground-level ray hit a roughly `0.10`-unit threshold while the wider movement corridor clipped the doorway frame.
- Separated attack line-of-fire from movement clearance. PF127 attacks now use the captured `+1.0 Y` center ray, while chase planning and route validation retain the six-probe body-width movement corridor.
- Added a shared PF127 combat leash. NPC homes register at activation; non-player-owned hostile NPCs disengage beyond `100` horizontal units from home, send `StopFight`, suppress aggro while returning, and use the same collision-aware route service to move home without teleporting.
- Leash reset clears Vergil healing state and cancels/despawns Abmouth's active combat-only Infector summons. Player-controlled pets are excluded.
- AORebirth.Core and ZoneEngine Debug builds pass. Navigation `36/36`, PF127 collision/LOS `17/17`, and Abmouth/Vergil `20/20` pass; lifecycle remains at the same six unrelated baseline guardrail failures.

## Remaining

1. Mike performs one private-client Vergil test: use the pictured open doorway and confirm he fires through the opening while real walls still stop damage.
2. Kite Vergil more than `100` horizontal units from his spawn and confirm he stops fighting, returns through the shared route path, and cannot immediately reacquire combat until home.
3. If client-visible movement differs from the deterministic route tests, inspect the existing server logs/capture evidence before changing planner limits, leash distance, or movement packets.
4. Do not auto-attach or launch AO/capture tooling. Mike runs gameplay and supplies completed captures when requested.

## Constraints

- The chase architecture is global, but PF127/resource `127` is the only enabled provider for this gameplay slice.
- The promoted collision asset and LOS gate fail closed when evidence or geometry is missing/invalid.
- The `100`-unit PF127 leash is a bounded private-server gameplay policy derived from the observed full-playfield chase; it is not claimed as an official-live maximum.
- Existing working Subway combat, loot, corpse, respawn, and population behavior must remain unchanged.
