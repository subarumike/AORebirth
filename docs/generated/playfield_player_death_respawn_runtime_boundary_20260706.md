# Playfield Player Death Respawn Runtime Boundary

Date: 2026-07-06

## Summary

`PlayfieldPlayerDeathRespawnRuntimeService` now owns the player death/respawn callback sequencing that was previously grouped under the generic playfield lifecycle service.

This is an ownership split only. Packet construction, stat mutation, destination lookup, teleport mechanics, and combat algorithms remain in their existing owners.

## Service-Owned Sequence

The service preserves the existing order:

1. Log skipped player corpse visual.
2. Send death-side social status packet callback.
3. Mark the player respawned through the Playfield stat mutation callback.
4. Send death/respawn state stat packet callback.
5. Stop movement.
6. Run player death combat cleanup callback.
7. Send changed stats callback.
8. Log respawn request.
9. Re-enable timers.
10. Attempt same-playfield respawn completion.
11. Transfer to the respawn playfield if same-playfield completion did not handle it.

## Intentionally Still Outside The Service

- Packet construction and emission.
- Character stat mutation.
- Destination/playfield lookup.
- Teleport and cross-playfield handoff mechanics.
- Corpse identity allocation.
- Combat, damage, and range algorithms.
- Database/object construction.

## Guardrail

`PlayfieldRuntimeSystemsFacadeOwnsSeparatedRuntimeCoordinators` asserts the service is constructed by `PlayfieldRuntimeSystems`, owns the callback order, and does not reference packet construction, object lookup, stat algorithms, or transport internals.
