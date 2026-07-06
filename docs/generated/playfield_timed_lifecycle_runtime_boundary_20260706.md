# Playfield Timed Lifecycle Runtime Boundary - 2026-07-06

This checkpoint documents the timed lifecycle orchestration slice moved out of
`Playfield` and behind `PlayfieldRuntimeSystems`.

## Boundary Added

`PlayfieldTimedLifecycleRuntimeService` owns heartbeat lifecycle sequencing
only. It decides the order for:

- pending corpse spawn/despawn/credit-award processing callbacks
- character heartbeat selection and skip checks
- dead NPC despawn processing before normal timers
- regeneration callback before combat callback
- NPC patrol callback versus non-NPC follow callback
- player collision callback after movement/follow processing

## Still Owned By Playfield

`Playfield` still owns the behavior behind those callbacks:

- health and nano regeneration stat mutation
- `SendChangedStats`
- combat tick entry and player/NPC combat callbacks
- follow controller invocation
- wall and statel collision checks
- corpse object collection mutation
- corpse full-update packet construction and emission
- corpse loot, credit, and inventory behavior

## Explicit Non-Goals

This slice does not change:

- attack, damage, range, or timing algorithms
- packet construction, packet serialization, or transport
- inventory, loot, credits, or corpse container behavior
- NPC runtime ownership or patrol algorithm internals
- database loading or object construction

## Guardrail

`PlayfieldLifecycleTraceTests.PlayfieldRuntimeSystemsFacadeOwnsSeparatedRuntimeCoordinators`
asserts that the timed lifecycle service owns orchestration while Playfield
retains algorithms and packet/world mutation callbacks.
