# Playfield Non-Combat Lifecycle Runtime Boundary - 2026-07-06

This checkpoint documents the next broad Playfield decomposition slice after the
timed heartbeat extraction.

## Boundary Added

`PlayfieldLifecycleRuntimeService` owns non-combat lifecycle sequencing for:

- player respawn state cleanup and handoff ordering
- player respawn combat-cleanup callback placement
- current-playfield respawn completion versus cross-playfield transfer choice
- playfield transfer pre-cleanup timing
- transfer contact-state cleanup before zoning
- timer disablement before playfield transfer handoff

`PlayfieldRuntimeSystems.ProcessHeartbeatTimedLifecycle` is the named facade for
heartbeat timed lifecycle scheduling after the prior timed-lifecycle extraction.

## Still Owned By Playfield

`Playfield` still owns:

- player respawn validation and destination resolution
- corpse identity allocation
- respawn logging text
- social status and changed-stat packet emission callbacks
- same-playfield respawn packet sequence construction and sends
- cross-playfield teleport, redirection, playfield lookup/creation, and detach
- statel/contact collection mutation callbacks

## Explicit Non-Goals

This slice does not change:

- attack, damage, range, or timing algorithms
- packet construction, packet serialization, or transport
- inventory, loot, credits, or corpse container behavior
- NPC runtime ownership
- database loading or object construction
